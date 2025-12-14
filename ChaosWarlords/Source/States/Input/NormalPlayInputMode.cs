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
        private readonly GameplayState _state;
        private readonly InputManager _inputManager;
        private readonly UIManager _uiManager;
        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public NormalPlayInputMode(GameplayState state, InputManager inputManager, UIManager uiManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
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

            // 2. Map and UI interaction takes precedence over map interaction
            if (inputManager.IsLeftMouseJustClicked())
            {
                // Check Action Buttons
                IGameCommand actionCommand = CheckActionButtons(inputManager, actionSystem);
                if (actionCommand != null) return actionCommand;

                // Check Market Button
                IGameCommand marketCommand = CheckMarketButton(inputManager);
                if (marketCommand != null) return marketCommand;

                // Check Map interaction (Handles deployment)
                return HandleMapInteraction(inputManager, mapManager, activePlayer);
            }

            return null;
        }

        private IGameCommand HandleCardInput(Point mousePos, InputManager inputManager, Player activePlayer) // <--- CHANGED RETURN TYPE AND ADDED ARGS
        {
            // The original logic iterated backwards to handle overlap, which is good.
            for (int i = activePlayer.Hand.Count - 1; i >= 0; i--) // <--- CHANGED TO activePlayer
            {
                var card = activePlayer.Hand[i]; // <--- CHANGED TO activePlayer

                // Use the passed-in inputManager
                if (inputManager.IsLeftMouseJustClicked() && card.Bounds.Contains(mousePos))
                {
                    // You are delegating to a method on GameplayState that calls a command,
                    // but we want to return a command directly to keep the InputMode clean.
                    // PlayCardCommand is the correct command to use here.
                    return new PlayCardCommand(card);
                }
            }
            return null; // Return null if no card was clicked
        }

        private IGameCommand HandleMapInteraction(InputManager inputManager, IMapManager mapManager, Player activePlayer) // <--- CHANGED RETURN TYPE AND ADDED ARGS
        {
            // Use the passed arguments
            var clickedNode = mapManager.GetNodeAt(inputManager.MousePosition);
            if (clickedNode != null)
            {
                // TryDeploy returns a command (or at least it should) if it's successful
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