using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Systems;


namespace ChaosWarlords.Source.States.Input
{
    public class NormalPlayInputMode : IInputMode
    {
        private readonly IGameplayState _state; // Interface is enough now
        private readonly IInputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public NormalPlayInputMode(IGameplayState state, IInputManager inputManager, IUIManager uiManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
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




