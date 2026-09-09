using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;

namespace ChaosWarlords.Source.Input.Modes
{
    /// <summary>
    /// Handles a forced discard (ActionState.TargetingDiscard) - Insane Outcast's own
    /// "discard a card from your hand" cost, and Neogi's per-opponent forced discard (the
    /// active player at click time is whoever must currently discard - see
    /// GameplayView/GameplayInputCoordinator, both of which already key off
    /// context.ActivePlayer, which MatchManager's forced-actor override points at the
    /// correct player during Neogi's cross-player sequencing).
    ///
    /// A forced discard with no cancel (matching Neogi's/Cranium Rats' design intent) -
    /// returning null for every non-LeftClick event here IS the enforcement: Escape/RightClick
    /// now route through GameplayInputCoordinator into this mode like any other input, and a
    /// null result correctly means "nothing to cancel," falling through to the pause-menu
    /// fallback at most (which never touches ActionSystem) rather than a raw
    /// ActionSystem.CancelTargeting() bypassing this mode entirely, as a global handler
    /// independent of the active mode once did. See planning.txt.
    /// </summary>
    public class DiscardInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState;
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;

        public DiscardInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;

            _gameplayState.Logger.Log("Select a card from your hand to discard.", LogChannel.General);
        }

        public IGameCommand? HandleInteraction(Core.Events.InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (evt.Type != Core.Events.InputEventType.LeftClick)
            {
                return null;
            }

            var targetCard = _gameplayState.GetHoveredHandCard();
            if (targetCard is null || targetCard.Id is null)
            {
                return null;
            }

            // Ambassador's "you may promote it instead" - only offered when this discard is
            // genuinely opponent-caused (matches DiscardCardCommand.Validate()'s own gate), never
            // for a voluntary own-hand discard (e.g. Insane Outcast paying its own cost) even if
            // that card happened to carry this reactive effect. Raises the same generic
            // Yes/No popup ActionSystem.OnInteractionRequested itself uses, then dispatches
            // whichever DiscardCardCommand the player's choice calls for - the choice itself
            // becomes part of the recorded/replayed command, not a client-only branch.
            bool offersPromoteInstead = targetCard.ReactiveDiscardEffect?.Type == EffectType.PromoteInsteadOfDiscard
                && _gameplayState.MatchContext.TurnManager.ForcedActingPlayer == activePlayer;

            if (offersPromoteInstead)
            {
                string cardId = targetCard.Id;
                _gameplayState.RequestOptionalEffect(
                    targetCard,
                    targetCard.ReactiveDiscardEffect!,
                    onAccept: () => _gameplayState.RecordAndExecuteCommand(new DiscardCardCommand(activePlayer.Color, cardId, promoteInsteadOfDiscard: true)),
                    onDecline: () => _gameplayState.RecordAndExecuteCommand(new DiscardCardCommand(activePlayer.Color, cardId, promoteInsteadOfDiscard: false)));
                return null;
            }

            return new DiscardCardCommand(activePlayer.Color, targetCard.Id);
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            // No continuous logic needed.
        }
    }
}
