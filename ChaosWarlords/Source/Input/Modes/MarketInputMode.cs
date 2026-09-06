using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using Microsoft.Xna.Framework.Input;


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
        private const int CooldownFrames = 5;

        public IGameCommand? HandleInteraction(Core.Events.InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // Right Click / Escape closes the market - treated as synonyms (matching
            // TargetingInputMode/PromoteInputMode's pattern). Used to reach here only via a
            // global, competing handler that ran before this mode ever got a chance to react -
            // see planning.txt. Checked BEFORE the cooldown gate below: that gate exists to
            // stop the SAME click that opened the market from also being read as a click
            // inside it (buy/close-on-empty-space), which doesn't apply to a keyboard
            // Escape/a distinct RightClick - gating those too would mean pressing Escape
            // within the cooldown window falls through to the pause-menu fallback instead of
            // closing the market.
            bool isCancelInput = evt.Type == Core.Events.InputEventType.RightClick
                || (evt.Type == Core.Events.InputEventType.KeyDown && evt.Key == Keys.Escape);
            if (isCancelInput)
            {
                return HandleCancellation();
            }

            if (_updateFrames < CooldownFrames) return null;

            if (evt.Type != Core.Events.InputEventType.LeftClick) return null;

            return HandleLeftClick();
        }

        private IGameCommand? HandleLeftClick()
        {
            var card = _state.GetHoveredMarketCard();

            // If market button is hovered, do nothing (keep market open)
            if (_uiManager.IsMarketHovered) return null;

            if (card != null)
            {
                // Check if we are in Devour Mode (Callback exists in Manager)
                var devourCallback = _state.MarketStateManager.DevourCallback;
                return devourCallback != null ? devourCallback.Invoke(card) : new BuyCardCommand(card);
            }

            // Clicked empty space - close market
            _state.MarketStateManager.Close();

            return null;
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            _updateFrames++;
        }

        /// <summary>
        /// Closes the market and signals "handled" via SwitchToNormalModeCommand - the actual
        /// mode swap happens as a side effect of MarketStateManager.Close()'s own ModeChanged
        /// event (GameplayInputCoordinator.HandleMarketModeChanged), not via this command's
        /// Execute() (a harmless, already-established redundant CancelTargeting() no-op when
        /// nothing is actually being targeted - see TargetingInputMode.HandleCancellation for
        /// the same pattern). Returning a non-null command here is what stops
        /// GameplayInputCoordinator's "nothing handled it" fallback from ALSO firing (e.g.
        /// opening the pause menu right after this closes the market).
        /// </summary>
        private SwitchToNormalModeCommand HandleCancellation()
        {
            _state.MarketStateManager.Close();
            return new SwitchToNormalModeCommand();
        }
    }
}
