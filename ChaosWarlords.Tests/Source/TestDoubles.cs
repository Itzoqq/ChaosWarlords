using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Tests
{
    // ------------------------------------------------------------------------
    // SHARED MOCKS
    // These are retained because they are still used by InputMode tests
    // ------------------------------------------------------------------------

    public class MockInputProvider : IInputProvider
    {
        // Backing fields
        public MouseState MouseState { get; private set; }
        public KeyboardState KeyboardState { get; private set; }

        public MockInputProvider()
        {
            // Initialize with default (Released, 0,0) states
            MouseState = new MouseState();
            KeyboardState = new KeyboardState();
        }

        // Interface Implementation
        public MouseState GetMouseState() => MouseState;
        public KeyboardState GetKeyboardState() => KeyboardState;

        // --- Helper Methods for Tests ---

        public void SetMouseState(MouseState state)
        {
            MouseState = state;
        }

        public void SetKeyboardState(KeyboardState state)
        {
            KeyboardState = state;
        }
    }

    public class MockUISystem : IUISystem
    {
        public bool IsMarketHovered { get; set; } = false;
        public bool IsAssassinateHovered { get; set; } = false;
        public bool IsReturnSpyHovered { get; set; } = false;
        public int ScreenWidth { get; } = 800;
        public int ScreenHeight { get; } = 600;

        // These can be assigned if a test needs them
        public Rectangle MarketButtonRect { get; set; } = Rectangle.Empty;
        public Rectangle AssassinateButtonRect { get; set; } = Rectangle.Empty;
        public Rectangle ReturnSpyButtonRect { get; set; } = Rectangle.Empty;
        public Rectangle EndTurnButtonRect { get; set; } = Rectangle.Empty;

        // Popup
        public Rectangle PopupBackgroundRect { get; set; } = Rectangle.Empty;
        public Rectangle PopupConfirmButtonRect { get; set; } = Rectangle.Empty;
        public Rectangle PopupCancelButtonRect { get; set; } = Rectangle.Empty;

        public bool IsEndTurnHovered { get; set; } = false;
        public bool IsPopupConfirmHovered { get; set; } = false;
        public bool IsPopupCancelHovered { get; set; } = false;

        public event EventHandler? OnMarketToggleRequest;
        public event EventHandler? OnAssassinateRequest;
        public event EventHandler? OnReturnSpyRequest;
#pragma warning disable CS0067
        public event EventHandler? OnEndTurnRequest;
        public event EventHandler? OnPopupConfirm;
        public event EventHandler? OnPopupCancel;
#pragma warning restore CS0067

        public void RaiseMarketToggle() => OnMarketToggleRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseAssassinateRequest() => OnAssassinateRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseReturnSpyRequest() => OnReturnSpyRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseEndTurnRequest() => OnEndTurnRequest?.Invoke(this, EventArgs.Empty);

        public void Update(InputManager input) { }
    }
}