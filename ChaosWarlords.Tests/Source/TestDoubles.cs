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

        public event EventHandler? OnMarketToggleRequest;
        public event EventHandler? OnAssassinateRequest;
        public event EventHandler? OnReturnSpyRequest;

        public void RaiseMarketToggle() => OnMarketToggleRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseAssassinateRequest() => OnAssassinateRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseReturnSpyRequest() => OnReturnSpyRequest?.Invoke(this, EventArgs.Empty);
        public void Update(InputManager input) { }
    }
}