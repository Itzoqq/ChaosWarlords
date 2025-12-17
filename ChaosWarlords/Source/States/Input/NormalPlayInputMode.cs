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
        private readonly IGameplayState _state;
        private readonly InputManager _inputManager;

        // FIX: Changed from UIManager to IUISystem
        private readonly IUISystem _uiManager;

        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public NormalPlayInputMode(IGameplayState state, InputManager inputManager, IUISystem uiManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
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

            IGameCommand cardCommand = HandleCardInput(mousePos, inputManager, activePlayer);
            if (cardCommand != null) return cardCommand;

            if (inputManager.IsLeftMouseJustClicked())
            {
                HandleMapInteraction(inputManager, mapManager, activePlayer);
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
                mapManager.TryDeploy(activePlayer, clickedNode);
            }
            return null;
        }
    }
}