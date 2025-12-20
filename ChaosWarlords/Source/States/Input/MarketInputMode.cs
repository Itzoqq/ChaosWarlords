using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Source.States.Input
{
    public class MarketInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly InputManager _inputManager;
        private readonly IUISystem _uiManager;
        private readonly IMarketManager _marketManager;
        private readonly TurnManager _turnManager;

        public MarketInputMode(IGameplayState state, InputManager inputManager, IUISystem uiManager, IMarketManager marketManager, TurnManager turnManager)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _marketManager = marketManager;
            _turnManager = turnManager;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // REMOVED: marketManager.Update(...) - this is now handled in GameplayState.Update

            if (!inputManager.IsLeftMouseJustClicked()) return null;

            if (_uiManager.IsMarketHovered) return null;

            // Get hovered card from View Model (via State)
            Card cardToBuy = _state.GetHoveredMarketCard();

            if (cardToBuy != null)
            {
                return new BuyCardCommand(cardToBuy);
            }

            // Clicked empty space? Close market.
            _state.CloseMarket();

            return null;
        }
    }
}