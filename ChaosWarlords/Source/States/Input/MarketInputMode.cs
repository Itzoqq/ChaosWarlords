using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.States.Input
{
    public class MarketInputMode : IInputMode
    {
        private readonly GameplayState _state;
        private readonly InputManager _inputManager;
        private readonly UIManager _uiManager;
        private readonly IMarketManager _marketManager;
        private readonly TurnManager _turnManager;

        public MarketInputMode(GameplayState state, InputManager inputManager, UIManager uiManager, IMarketManager marketManager, TurnManager turnManager)
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
                // The TryBuyCard returns a command, but since you don't return one here,
                // let's assume it logs the success/failure and returns null.
                bool success = marketManager.TryBuyCard(activePlayer, cardToBuy);
                if (success) GameLogger.Log($"Bought {cardToBuy.Name}.", LogChannel.Economy);
            }

            // Check if the Market button was clicked (to close the market)
            // NOTE: This class doesn't have a direct reference to UIManager
            // I'm assuming you will handle closing via the ESC key or right-click in HandleGlobalInput,
            // which is executed BEFORE InputMode.HandleInput. 
            // For now, let's keep the logic based on clicking empty space.

            // If a card wasn't bought, close the market if the user clicked empty space.
            if (!clickedOnCard)
            {
                _state.CloseMarket();
            }

            return null;
        }
    }
}