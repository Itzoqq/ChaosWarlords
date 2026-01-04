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


namespace ChaosWarlords.Source.Input.Modes
{
    public class MarketInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly IInputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMarketManager _marketManager;
        private readonly Action<Card>? _onCardSelected; // Callback for custom actions (like Devour)

        private MatchContext _context;

        public MarketInputMode(IGameplayState state, IInputManager input, MatchContext context, Action<Card>? onCardSelected = null)
        {
            _context = context;
            _state = state;
            _inputManager = input;
            _onCardSelected = onCardSelected;

            _uiManager = state.UIManager;
            _marketManager = context.MarketManager;
        }

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (!inputManager.IsLeftMouseJustClicked()) return null;

            if (_uiManager.IsMarketHovered) return null;

            // Get hovered card from View Model (via State)
            Card? hoveredCard = _state.GetHoveredMarketCard();

            if (hoveredCard is not null)
            {
                if (_onCardSelected != null)
                {
                    // Custom action (Devour)
                    _onCardSelected(hoveredCard);
                    // Return null or NoOp because the action is handled via callback? 
                    // Usually input modes return commands or modify state. 
                    // If we use callback, we might need to signal "Task Done".
                    // For now, returning null is fine if the callback handles the state transition (e.g. ActionSystem.CompleteTargeting).
                    return null; 
                }
                else
                {
                    // Default action (Buy)
                    return new BuyCardCommand(hoveredCard);
                }
            }

            // Clicked empty space? Close market.
            _state.CloseMarket();

            return null;
        }
    }
}




