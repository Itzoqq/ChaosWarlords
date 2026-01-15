using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;


namespace ChaosWarlords.Source.Input.Modes
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
            _marketManager = context.MarketManager; // Keep this as it's used in the original class
        }

        // Removed constructor with callback - logic moved to HandleInput via MarketStateManager

        private int _updateFrames;
        private const int COOLDOWN_FRAMES = 5;

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            _updateFrames++;
            if (_updateFrames < COOLDOWN_FRAMES) return null;

            var mouseState = _inputManager.GetMouseState();
            var card = _state.GetHoveredMarketCard();

            // Left Click Handling
            if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _inputManager.IsLeftMouseJustClicked())
            {
                // If market button is hovered, do nothing (keep market open)
                if (_uiManager.IsMarketHovered) return null;

                if (card != null)
                {
                    // Check if we are in Devour Mode (Callback exists in Manager)
                    var devourCallback = _state.MarketStateManager.DevourCallback;
                    if (devourCallback != null)
                    {
                        return devourCallback.Invoke(card);
                        // Market logic is handled by the command execution via ActionSystem/MatchManager
                    }
                    else
                    {
                        return new BuyCardCommand(card);
                    }

                }

                // Clicked empty space - close market
                _state.MarketStateManager.Close();
            }

            return null;
        }
    }
}
