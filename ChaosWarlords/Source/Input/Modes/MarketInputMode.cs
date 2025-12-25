using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Source.States.Input
{
    public class MarketInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly InputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMarketManager _marketManager;
        private readonly TurnManager _turnManager;
        private MatchContext _context;

        public MarketInputMode(IGameplayState state, InputManager input, MatchContext context)
        {
            _context = context;
            _state = state;
            _inputManager = input;

            _uiManager = state.UIManager;

            _marketManager = context.MarketManager;
            _turnManager = context.TurnManager as TurnManager;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
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