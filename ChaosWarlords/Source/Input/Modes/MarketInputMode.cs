using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;


namespace ChaosWarlords.Source.States.Input
{
    public class MarketInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly IInputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMarketManager _marketManager;

        private MatchContext _context;

        public MarketInputMode(IGameplayState state, IInputManager input, MatchContext context)
        {
            _context = context;
            _state = state;
            _inputManager = input;

            _uiManager = state.UIManager;

            _marketManager = context.MarketManager;
        }

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (!inputManager.IsLeftMouseJustClicked()) return null;

            if (_uiManager.IsMarketHovered) return null;

            // Get hovered card from View Model (via State)
            Card? cardToBuy = _state.GetHoveredMarketCard();

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




