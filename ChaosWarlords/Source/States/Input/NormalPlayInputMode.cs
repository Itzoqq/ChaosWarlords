using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.States.Input
{
    public class NormalPlayInputMode : IInputMode
    {
        private readonly IGameplayState _state; // Interface is enough now
        private readonly InputManager _inputManager;
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
            if (inputManager.IsLeftMouseJustClicked())
            {
                // 1. Check Card Click
                Card clickedCard = _state.GetHoveredHandCard();
                if (clickedCard != null)
                {
                    return new PlayCardCommand(clickedCard);
                }

                // 2. Check Map Click
                var clickedNode = mapManager.GetNodeAt(inputManager.MousePosition);
                if (clickedNode != null)
                {
                    mapManager.TryDeploy(activePlayer, clickedNode);
                }
            }

            return null;
        }
    }
}