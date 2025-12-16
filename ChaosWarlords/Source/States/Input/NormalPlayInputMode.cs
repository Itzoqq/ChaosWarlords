using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.States.Input
{
    public class NormalPlayInputMode : IInputMode
    {
        // FIX 1: Change private field type to the interface
        private readonly IGameplayState _state;
        private readonly InputManager _inputManager;
        private readonly UIManager _uiManager;
        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        // FIX 2: Change constructor parameter type to the interface
        public NormalPlayInputMode(IGameplayState state, InputManager inputManager, UIManager uiManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            Point mousePos = inputManager.MousePosition.ToPoint();

            // 1. Check for Card Input (Playing a card returns a command if it targets)
            IGameCommand cardCommand = HandleCardInput(mousePos, inputManager, activePlayer);
            if (cardCommand != null) return cardCommand;

            // 2. Check for Market/Action Button Input
            if (inputManager.IsLeftMouseJustClicked())
            {
                // Check map interactions and action buttons
                IGameCommand buttonCommand = CheckMarketButton(inputManager);
                if (buttonCommand != null) return buttonCommand;

                buttonCommand = CheckActionButtons(inputManager, actionSystem);
                if (buttonCommand != null) return buttonCommand;

                // Handle map deployment logic last
                return HandleMapInteraction(inputManager, mapManager, activePlayer);
            }

            return null;
        }

        private IGameCommand HandleCardInput(Point mousePos, InputManager inputManager, Player activePlayer)
        {
            for (int i = activePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = activePlayer.Hand[i];

                if (inputManager.IsLeftMouseJustClicked() && card.Bounds.Contains(mousePos))
                {
                    return new PlayCardCommand(card);
                }
            }
            return null;
        }

        private IGameCommand HandleMapInteraction(InputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            var clickedNode = mapManager.GetNodeAt(inputManager.MousePosition);
            if (clickedNode != null)
            {
                // For now, we assume this logic *is* the command logic:
                mapManager.TryDeploy(activePlayer, clickedNode); // <--- Use passed parameters
            }
            return null; // For now, we don't return a command on deploy
        }

        private IGameCommand CheckMarketButton(InputManager inputManager) // <--- CHANGED RETURN TYPE AND ADDED ARG
        {
            if (_uiManager.IsMarketButtonHovered(inputManager)) // Use the passed inputManager
            {
                // This is a state change, so we return the appropriate command
                return new ToggleMarketCommand();
            }
            return null;
        }

        private IGameCommand CheckActionButtons(InputManager inputManager, IActionSystem actionSystem)
        {
            if (_uiManager.IsAssassinateButtonHovered(inputManager))
            {
                // FIX: Return the command to be executed.
                // The command will call actionSystem.TryStartAssassinate() in its Execute method.
                return new StartAssassinateCommand();
            }
            if (_uiManager.IsReturnSpyButtonHovered(inputManager))
            {
                // FIX: Return the command to be executed.
                return new StartReturnSpyCommand();
            }
            return null; // Return null if no button was clicked
        }
    }
}