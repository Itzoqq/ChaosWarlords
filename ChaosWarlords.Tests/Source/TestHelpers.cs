using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Tests
{
    // ------------------------------------------------------------------------
    // TEST HELPERS
    // Industry-standard test utilities for input simulation
    // ------------------------------------------------------------------------

    /// <summary>
    /// Test helper for simulating input states.
    /// This is NOT a mock - it's a test builder/helper for stateful input simulation.
    /// Industry precedent: Unity's InputTestFixture, Unreal's FAutomationTestBase
    /// </summary>
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
}