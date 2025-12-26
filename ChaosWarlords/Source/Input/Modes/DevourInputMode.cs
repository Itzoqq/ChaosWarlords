using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Interfaces;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.States.Input
{
    public class DevourInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState;
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;
        private readonly Card _sourceCard; // The card causing the devour (to prevent self-devour)

        public DevourInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;
            _sourceCard = actionSystem.PendingCard; // Capture which card triggered this

            GameLogger.Log("Select a card from your HAND to Devour (Remove from game).", LogChannel.General);
        }

        public IGameCommand HandleInput(IInputManager input, IMarketManager market, IMapManager map, Player activePlayer, IActionSystem actionSystem)
        {
            // 1. Cancel / Back out
            if (input.IsRightMouseJustClicked() || input.IsKeyJustPressed(Keys.Escape))
            {
                actionSystem.CancelTargeting();
                _gameplayState.SwitchToNormalMode();
                GameLogger.Log("Cancelled Devour action.", LogChannel.General);
                return null;
            }

            // 2. Select Card
            if (input.IsLeftMouseJustClicked())
            {
                // We specifically look at the HAND, not Played cards
                Card targetCard = _gameplayState.GetHoveredHandCard();

                if (targetCard != null)
                {
                    // Validation: Cannot devour the card itself while it's being played
                    if (targetCard == _sourceCard)
                    {
                        GameLogger.Log("Invalid Target: Cannot devour the card currently being played!", LogChannel.Warning);
                        return null;
                    }

                    // Execute logic directly or return a Command
                    // Using direct execution for simplicity as per your pattern:
                    _gameplayState.MatchManager.DevourCard(targetCard);

                    // Complete Action
                    actionSystem.CompleteAction();
                    // The ActionCompleted event in GameplayState will handle switching back to NormalMode
                }
            }

            return null;
        }
    }
}

