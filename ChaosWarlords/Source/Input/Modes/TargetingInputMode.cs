using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Rendering;
using ChaosWarlords.Source.Input;

namespace ChaosWarlords.Source.Input.Modes
{
    public class TargetingInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly IInputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMapManager _mapManager;
        private readonly ITurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public TargetingInputMode(IGameplayState state, IInputManager inputManager, IUIManager uiManager, IMapManager mapManager, ITurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand? HandleInteraction(InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // 1. SAFETY: State Desync Protection
            if (actionSystem.CurrentState == ActionState.Normal)
            {
                return new SwitchToNormalModeCommand();
            }

            // 2. UI Blocking
            if (IsUIBlocking())
            {
                return null;
            }

            // Targeting can be cancelled via Right Click or Escape - treated as synonyms
            // (matching PromoteInputMode/DevourInputMode/PromoteFromPileInputMode's existing
            // pattern). Escape used to reach this mode only via a global, competing handler
            // that bypassed this mode's own cancel/decline logic entirely - see planning.txt.
            bool isCancelInput = evt.Type == InputEventType.RightClick
                || (evt.Type == InputEventType.KeyDown && evt.Key == Keys.Escape);
            if (isCancelInput)
            {
                return HandleCancellation(actionSystem);
            }

            if (evt.Type != InputEventType.LeftClick) return null;

            // 1. Check Card Selection (if targeting cards, e.g. for some spells?)
            // Currently targeting usually means MAP nodes or SITES.

            // 2. Delegate to ActionSystem to validate the Clicked Target
            // We pass the Event Position
            return ConvertClickToCommand(evt.Position, mapManager, actionSystem);
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            // Helper text or hover highlights could be managed here
        }

        private static IGameCommand? ConvertClickToCommand(Vector2 clickPos, IMapManager mapManager, IActionSystem actionSystem)
        {
            // SelectingSpyToReturn (picking WHICH of 2+ differently-colored enemy spies at a
            // site to return) is PlayerController's sole responsibility - it has the
            // InteractionMapper-based hit-testing this class doesn't. This class and
            // PlayerController are 2 independent subscribers on the same InputManager.
            // OnInputEvent stream, with no "handled" flag shared between them, so this method
            // must stay a pure no-op for this state regardless of where the click lands -
            // reacting to it here at all would race with PlayerController's own handling of
            // the exact same click. See planning.txt.
            if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                return null;
            }

            var clickLogicPos = clickPos.ToLogicVector2();
            MapNode? targetNode = mapManager.GetNodeAt(clickLogicPos);
            Site? targetSite = mapManager.GetSiteAt(clickLogicPos);

            // Return the command if the click resolved an action
            return HandleTargetingClick(actionSystem, targetNode, targetSite);
        }

        // NOTE: these 3 hover flags can now only be true while their button's own IsActive()
        // is true, which requires !ActionSystem.IsTargeting() (see UIManager's button setup) -
        // but this method only ever runs while TargetingInputMode itself is the active mode,
        // i.e. while IsTargeting() is already true. So in practice none of the 3 can be true
        // here anymore (barring an unverified one-frame stale-hover window right at the
        // transition into targeting) - effectively inert, not removed since a future change to
        // either side's semantics could make it load-bearing again. See planning.txt.
        private bool IsUIBlocking()
        {
            return _uiManager.IsMarketHovered || _uiManager.IsAssassinateHovered || _uiManager.IsReturnSpyHovered;
        }

        private IGameCommand? HandleCancellation(IActionSystem actionSystem)
        {
            // "Move up to 2 enemy troops" (Council Member) etc. - right-click at a genuine
            // repeat boundary means "I'm done, keep what I already did," NOT "undo the whole
            // card play" - dispatch DeclineRepeatCommand (a real, replay-recorded command)
            // instead of the usual CancelTargeting() full revert. See CardEffect.
            // AllowPartialRepeat and DeclineRepeatCommand's own doc comment.
            var effect = actionSystem.CurrentEffect;
            if (IsAtADeclinableRepeatBoundary(effect, actionSystem))
            {
                _state.Logger.Log($"Input: Declining remaining repeats for {effect!.SourceCard?.Name ?? "Unknown"}.", LogChannel.Info);
                return new DeclineRepeatCommand(effect.SourceCard?.Id);
            }

            // Not at the boundary, but might still be genuinely mid-way through THIS SAME
            // repeat-capable effect's own multi-click sub-pick (e.g. MoveUnit's source chosen,
            // destination not yet picked) - "I'm done" isn't what a right-click here should
            // mean (nothing's actually finished yet for this repeat), so step back to the
            // entry state instead of fully cancelling the whole card play. Purely a client-side
            // reset (no command dispatched, nothing replay-significant happened) - the
            // redemption stays open. See planning.txt's Phase 2 writeup for why this differs
            // from the explicit "I'm done" button, which always declines unconditionally
            // instead.
            if (actionSystem.TryAbortInProgressRepeatSubStep())
            {
                _state.Logger.Log("Input: Stepped back from an in-progress repeat sub-target - redemption still open.", LogChannel.Info);
                return null;
            }

            // Safety Log
            string cardName = actionSystem.PendingCard is not null ? actionSystem.PendingCard.Name : "Unknown";
            _state.Logger.Log($"Input: Cancelled Action for {cardName}. Card returned to hand.", LogChannel.Info);

            actionSystem.CancelTargeting();
            // We return this command to ensure immediate update,
            // though the event system could handle cancellation too if you wired OnActionCancelled.
            return new SwitchToNormalModeCommand();
        }

        /// <summary>
        /// True when right-click should decline a repeat-optional effect's remaining repeats
        /// instead of fully cancelling: a genuine repeat boundary (CurrentState still equals
        /// the pending effect's own entry state), not mid a multi-click sub-target like
        /// MoveUnit's source/destination pair.
        /// </summary>
        private static bool IsAtADeclinableRepeatBoundary(ChaosWarlords.Source.Core.Contexts.EffectContext? effect, IActionSystem actionSystem)
        {
            return effect?.SourceEffect?.AllowPartialRepeat == true && actionSystem.CurrentState == effect.EffectType;
        }

        private static IGameCommand? HandleTargetingClick(IActionSystem actionSystem, MapNode? targetNode, Site? targetSite)
        {
            if (targetNode is null && targetSite is null)
            {
                return null;
            }

            var command = actionSystem.HandleTargetClick(targetNode, targetSite);

            if (command == null)
            {
                return null;
            }

            return IsPreCommitFlow(actionSystem)
                ? HandlePreCommitTargeting(actionSystem, targetNode, targetSite)
                : command;
        }

        private static bool IsPreCommitFlow(IActionSystem actionSystem)
        {
            return actionSystem.PendingCard != null && actionSystem.PendingCard.Location == CardLocation.Hand;
        }

        private static PlayCardCommand? HandlePreCommitTargeting(IActionSystem actionSystem, MapNode? targetNode, Site? targetSite)
        {
            var pendingCard = actionSystem.PendingCard!;
            object target = (object?)targetNode ?? targetSite!;

            actionSystem.SetPreTarget(pendingCard, actionSystem.CurrentState, target);

            if (actionSystem.AdvancePreCommitTargeting(pendingCard))
            {
                // Advanced to next targeting state
                return null;
            }

            // Chain complete - commit the play
            return new PlayCardCommand(pendingCard, true);
        }
    }
}
