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
    /// Intended as a forced discard with no cancel (matching Neogi's/Cranium Rats' design
    /// intent), but this is NOT currently enforced - PlayerController.HandleGlobalInput's
    /// global right-click/Escape handler will cancel out of ANY non-Normal ActionState,
    /// including this one, regardless of input mode. See planning.txt for the follow-up this
    /// gap needs (a real ActionState-aware cancel gate), not fixed here.
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

            return new DiscardCardCommand(activePlayer.Color, targetCard.Id);
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            // No continuous logic needed.
        }
    }
}
