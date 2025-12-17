using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;


namespace ChaosWarlords.Source.States.Input
{
    public class MarketInputMode : IInputMode
    {
        private readonly IGameplayState _state;

        // These fields are kept as they were in the original file
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
            marketManager.Update(inputManager.MousePosition);

            if (!inputManager.IsLeftMouseJustClicked()) return null;

            // If we are clicking the Market Button (to toggle), ignore it here.
            // The UI Manager handles the button click. We don't want to interpret it as "Close Menu".
            if (_uiManager.IsMarketHovered) return null;

            bool clickedOnCard = false;
            Card cardToBuy = null;

            foreach (var card in marketManager.MarketRow)
            {
                if (card.IsHovered)
                {
                    clickedOnCard = true;
                    cardToBuy = card;
                    break;
                }
            }

            if (cardToBuy != null)
            {
                return new BuyCardCommand(cardToBuy);
            }

            // Only close if we clicked strictly on "Empty Space" (not a card, not the button)
            if (!clickedOnCard)
            {
                _state.CloseMarket();
            }

            return null;
        }
    }
}