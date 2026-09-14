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

        // amountToPromote is purely informational (the initial log line below) - "when to
        // stop" is decided live off TurnContext.PendingPromotionsCount (see HandleLeftClick),
        // not a separately-maintained counter that could drift from the real credit ledger.
        public PromoteInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem, int amountToPromote)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;

            _gameplayState.Logger.Log($"Select {amountToPromote} card(s) from your PLAYED pile to Promote.", LogChannel.General);
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
        /// whatever's left. A plain "promote a card played this turn" (Wyrmspeaker) has no such
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
            // Rejects a card promoting itself, or (Air/Fire/Water Elemental Myrmidon's aspect-
            // filtered credits) a card whose Aspect doesn't match the credit's own filter - see
            // TurnContext.HasValidCreditFor/CreditAllows. A friendly early rejection with a
            // specific log message; PromoteCommand.Validate() re-derives the exact same check
            // as the real authorization boundary (planning.txt TIER 1 item 15), so this isn't
            // this flow's only defense.
            if (!context.HasValidCreditFor(targetCard))
            {
                _gameplayState.Logger.Log("Invalid Target: no outstanding promotion credit can promote this card (itself, or the wrong aspect).", LogChannel.Warning);
                return null;
            }

            _gameplayState.Logger.Log($"Promoted {targetCard.Name} to Inner Circle!", LogChannel.Economy);

            // Dispatch the promote command - PromoteCommand.Execute() itself now consumes the
            // matching credit and sweeps any now-unsatisfiable sibling (e.g. Air + Fire
            // Elemental Myrmidon both requiring an Obedience card, with only one actually
            // played this turn), rather than this input mode doing it BEFORE dispatch. That
            // used to mean a replayed/directly-dispatched PromoteCommand (replay never runs
            // input modes - see GameplayState.SwitchToTargetingMode) silently never consumed a
            // credit at all - see PromoteCommand.Execute's own doc comment.
            var promoteCmd = new Commands.PromoteCommand(targetCard.Id);
            _gameplayState.RecordAndExecuteCommand(promoteCmd);

            // Check if we are done by reading the credit ledger directly (now authoritative,
            // since Execute() above already consumed/forfeited as needed) rather than a
            // separately-maintained counter that could drift from it. NOT CancelTargeting()
            // here either (same reasoning as HandleCancellation above): every credit in this
            // redemption may have already promoted a real card via an earlier left-click in
            // this same loop, and CancelTargeting()'s full-sequence snapshot revert (taken once,
            // before the FIRST promotion) would silently undo ALL of them, not just "finish
            // cleanly."
            if (context.PendingPromotionsCount <= 0)
            {
                actionSystem.DeclineRemainingPromotions();

                // Return EndTurn command to be executed by Coordinator immediately after
                return new Commands.EndTurnCommand();
            }

            // If not done, return null (command already executed above)
            return null;
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            // No continuous update needed for now
        }
    }
}
