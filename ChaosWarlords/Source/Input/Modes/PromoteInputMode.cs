using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Input.Modes
{
    public class PromoteInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState; // Changed type to concrete to access EndTurn easily
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;
        private int _cardsLeftToPromote;

        public PromoteInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem, int amountToPromote)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;
            _cardsLeftToPromote = amountToPromote;

            _gameplayState.Logger.Log($"Select {_cardsLeftToPromote} card(s) from your PLAYED pile to Promote.", LogChannel.General);
        }

        public IGameCommand? HandleInteraction(Core.Events.InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            bool isCancelInput = evt.Type == Core.Events.InputEventType.RightClick
                || (evt.Type == Core.Events.InputEventType.KeyDown && evt.Key == Keys.Escape);

            if (isCancelInput)
            {
                return HandleCancellation(actionSystem);
            }

            if (evt.Type == Core.Events.InputEventType.LeftClick)
            {
                return HandleLeftClick(actionSystem);
            }

            return null;
        }

        /// <summary>
        /// Right-click/Escape: "at end of turn, promote up to 2 other cards played this turn"
        /// (Cultist of Myrkul, Zuggtmoy) is genuinely optional - the player may forfeit
        /// whatever's left. A plain "promote a card played this turn" (core_noble) has no such
        /// wording and stays mandatory (tyrants-rules.pdf p.9's plain instruction-following
        /// rule) - only decline early when EVERY outstanding credit is declinable, never
        /// partway through a still-mandatory one. Uses ActionSystem.DeclineRemainingPromotions()
        /// here, NOT CancelTargeting() - this session's redemption may have already promoted
        /// real cards via earlier left-clicks, and CancelTargeting()'s full-sequence snapshot
        /// revert (taken once, before the FIRST promotion) would silently undo those too, not
        /// just the declined remainder. See ActionSystem.DeclineRemainingPromotions's own doc
        /// comment.
        /// </summary>
        private Commands.EndTurnCommand? HandleCancellation(IActionSystem actionSystem)
        {
            var context = _gameplayState.MatchContext.TurnManager.CurrentTurnContext;
            if (context.CanDeclineRemainingPromotions)
            {
                _gameplayState.Logger.Log("Declining remaining optional promotion credit(s).", LogChannel.Info);
                context.ForfeitRemainingPromotions();
                actionSystem.DeclineRemainingPromotions();
                return new Commands.EndTurnCommand();
            }

            _gameplayState.Logger.Log("Mandatory Action: You must select a card to promote.", LogChannel.Warning);
            return null;
        }

        private Commands.EndTurnCommand? HandleLeftClick(IActionSystem actionSystem)
        {
            Card? targetCard = _gameplayState.GetHoveredPlayedCard();
            if (targetCard is null)
            {
                return null;
            }

            var context = _gameplayState.MatchContext.TurnManager.CurrentTurnContext;

            // --- Safety Check ---
            // Prevent a card from promoting itself if it is the only source of points
            if (!context.HasValidCreditFor(targetCard))
            {
                _gameplayState.Logger.Log("Invalid Target: This card cannot promote itself!", LogChannel.Warning);
                return null;
            }

            _cardsLeftToPromote--;
            _gameplayState.Logger.Log($"Promoted {targetCard.Name} to Inner Circle!", LogChannel.Economy);

            context.ConsumeCreditFor(targetCard);

            // 1. Manually execute the promote command immediately
            var promoteCmd = new Commands.PromoteCommand(targetCard.Id);
            _gameplayState.RecordAndExecuteCommand(promoteCmd);

            // 2. Check if we are done - NOT CancelTargeting() here either (same reasoning as
            // HandleCancellation above): every credit in this redemption may have already
            // promoted a real card via an earlier left-click in this same loop, and
            // CancelTargeting()'s full-sequence snapshot revert (taken once, before the FIRST
            // promotion) would silently undo ALL of them, not just "finish cleanly."
            if (_cardsLeftToPromote <= 0)
            {
                actionSystem.DeclineRemainingPromotions();

                // 3. Return EndTurn command to be executed by Coordinator immediately after
                return new Commands.EndTurnCommand();
            }

            // 4. If not done, return null (Command already executed above)
            return null;
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            // No continuous update needed for now
        }
    }
}
