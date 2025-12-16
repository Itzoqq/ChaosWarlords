using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.States; // Added using for IGameplayState

namespace ChaosWarlords.Source.States.Input
{
    public class MarketInputMode : IInputMode
    {
        // FIX 1: Change private field type to the interface
        private readonly IGameplayState _state;

        // These fields are kept as they were in the original file
        private readonly InputManager _inputManager;
        private readonly UIManager _uiManager;
        private readonly IMarketManager _marketManager;
        private readonly TurnManager _turnManager;

        // FIX 2: Change constructor parameter type to the interface
        public MarketInputMode(IGameplayState state, InputManager inputManager, UIManager uiManager, IMarketManager marketManager, TurnManager turnManager)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _marketManager = marketManager;
            _turnManager = turnManager;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // Update market visuals/hover states
            marketManager.Update(inputManager.MousePosition);

            if (!inputManager.IsLeftMouseJustClicked()) return null;

            bool clickedOnCard = false;
            Card cardToBuy = null;

            // Check if a market card was clicked (hovered)
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
                // This command will attempt to buy the card.
                return new BuyCardCommand(cardToBuy);
            }

            // Check if the Market button was clicked (to close the market)
            // Note: This logic for closing is a duplicate of CheckMarketButton in NormalPlayMode 
            // but is often used in the Market context to quickly close it.
            if (_uiManager.IsMarketButtonHovered(inputManager))
            {
                return new ToggleMarketCommand();
            }

            // If a card wasn't bought and no button was clicked, and we are not hovering the market button,
            // we assume the user clicked empty space and closes the market.
            if (!clickedOnCard)
            {
                // FIX: Use the method exposed by the interface
                _state.CloseMarket();
            }

            return null;
        }
    }
}