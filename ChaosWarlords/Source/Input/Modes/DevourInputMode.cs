using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Source.Input.Modes
{
    public class DevourInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState;
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;
        private readonly Card? _sourceCard; // The card causing the devour (to prevent self-devour)

        public DevourInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;
            _sourceCard = actionSystem.PendingCard; // Capture which card triggered this

            if (actionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                _gameplayState.Logger.Log("Select a card from your INNER CIRCLE to Devour.", LogChannel.General);
            }
            else
            {
                _gameplayState.Logger.Log("Select a card from your HAND to Devour (Remove from game).", LogChannel.General);
            }
        }

        private int _updateFrames;
        private const int COOLDOWN_FRAMES = 10; // Slightly longer to ensure popup click is fully cleared

        public IGameCommand? HandleInteraction(Core.Events.InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (_updateFrames < COOLDOWN_FRAMES) return null;

            // Handle Cancellation (Right Click or Escape)
            if (evt.Type == Core.Events.InputEventType.RightClick || (evt.Type == Core.Events.InputEventType.KeyDown && evt.Key == Keys.Escape))
            {
                return HandleCancellation(actionSystem);
            }

            // Handle Optional Skip (Space)
            if (evt.Type == Core.Events.InputEventType.KeyDown && evt.Key == Keys.Space)
            {
                return HandleSkipOptionalCost(actionSystem);
            }

            // Handle Selection (Left Click)
            if (evt.Type == Core.Events.InputEventType.LeftClick)
            {
                return HandleCardClick(actionSystem);
            }

            return null;
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            _updateFrames++;
            // Could add hover logic here if needed
        }



        private Commands.PlayCardCommand? HandleSkipOptionalCost(IActionSystem actionSystem)
        {
            if (!IsPreCommitFlow())
            {
                return null; // Skip only supported for pre-commit flow
            }

            actionSystem.SetPreTarget(_sourceCard!, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            if (actionSystem.AdvancePreCommitTargeting(_sourceCard!))
            {
                // Advanced to next targeting state
                return null;
            }

            // No more pre-commit targeting needed. Do NOT call CompleteAction() here - the
            // card hasn't been played yet (still sits in Hand; AdvancePreCommitTargeting only
            // clears DevourSubsystem's own state, never touches ExecutionStack), so
            // CompleteAction() would hit its "no stack context" fallback and fire
            // OnActionCompleted prematurely, before the card is even played. That used to
            // work by accident, via UIEventMediator.HandleActionCompleted's PendingCard
            // re-entrant PlayCard call, but only because the card was still genuinely in Hand
            // at that moment - a landmine masquerading as a feature. The PlayCardCommand
            // below is the real, single commit path: it runs the card's effects fresh
            // (ResolveEffects), and TryExecutePreTargetEffect picks up the pre-targets set
            // above to auto-resolve them without re-prompting. OnActionCompleted then fires
            // naturally once that real chain empties the stack (see planning.txt).
            _gameplayState.SwitchToNormalMode();
            return new Commands.PlayCardCommand(_sourceCard!, true);
        }

        private IGameCommand? HandleCardClick(IActionSystem actionSystem)
        {
            Card? targetCard = null;

            if (actionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                targetCard = _gameplayState.GetHoveredBrowserCard();
            }
            else
            {
                // Default to Hand
                targetCard = _gameplayState.GetHoveredHandCard();
            }

            if (targetCard is null)
            {
                return null;
            }

            if (!IsValidDevourTarget(targetCard))
            {
                return null;
            }

            if (actionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                var cmd = actionSystem.HandleDevourInnerCircleSelection(targetCard);
                return cmd;
            }

            return IsPreCommitFlow()
                ? HandlePreCommitSelection(targetCard, actionSystem)
                : HandleStandardFlowSelection(targetCard);
        }

        private bool IsPreCommitFlow()
        {
            return _sourceCard != null && _sourceCard.Location == CardLocation.Hand;
        }

        private bool IsValidDevourTarget(Card targetCard)
        {
            if (targetCard == _sourceCard)
            {
                _gameplayState.Logger.Log("Invalid Target: Cannot devour the card currently being played!", LogChannel.Warning);
                return false;
            }
            return true;
        }

        private Commands.PlayCardCommand? HandlePreCommitSelection(Card targetCard, IActionSystem actionSystem)
        {
            actionSystem.SetPreTarget(_sourceCard!, ActionState.TargetingDevourHand, targetCard);

            if (actionSystem.AdvancePreCommitTargeting(_sourceCard!))
            {
                // Advanced to next targeting state
                return null;
            }

            // Chain complete, commit the play. See HandleSkipOptionalCost's comment above for
            // why this deliberately does NOT call actionSystem.CompleteAction() first.
            _gameplayState.SwitchToNormalMode();
            return new Commands.PlayCardCommand(_sourceCard!, true);
        }

        private Commands.DevourCardCommand? HandleStandardFlowSelection(Card targetCard)
        {
            var cmd = _actionSystem.HandleDevourSelection(targetCard);

            if (_actionSystem.IsTargeting())
            {
                _gameplayState.SwitchToTargetingMode();
            }
            else
            {
                _gameplayState.SwitchToNormalMode();
            }

            return cmd;
        }

        private Commands.SwitchToNormalModeCommand HandleCancellation(IActionSystem actionSystem)
        {
            _gameplayState.Logger.Log("Devour cancelled. Card returned to hand.", LogChannel.Info);
            actionSystem.CancelTargeting();
            
            // Explicitly switch state back to avoid stuck states
            _gameplayState.SwitchToNormalMode();

            return new Commands.SwitchToNormalModeCommand();
        }
    }
}




