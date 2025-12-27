using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
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

    // ------------------------------------------------------------------------
    // INPUT SIMULATION HELPERS
    // Static utilities to reduce code duplication across test suites
    // ------------------------------------------------------------------------

    /// <summary>
    /// Static helper methods for simulating input in tests.
    /// Reduces repetitive 4-line patterns to single method calls.
    /// </summary>
    public static class InputTestHelpers
    {
        /// <summary>
        /// Simulates a left mouse button click at the specified position.
        /// This is the most common input pattern in tests (15+ usages).
        /// </summary>
        /// <param name="input">The mock input provider</param>
        /// <param name="manager">The input manager to update</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SimulateLeftClick(MockInputProvider input, IInputManager manager, int x, int y)
        {
            // Set released state first
            input.SetMouseState(CreateReleasedMouseState(x, y));
            manager.Update();
            
            // Then set pressed state
            input.SetMouseState(CreateMouseState(x, y, left: ButtonState.Pressed));
            manager.Update();
        }

        /// <summary>
        /// Simulates a right mouse button click at the specified position.
        /// Commonly used for cancel/context menu operations.
        /// </summary>
        /// <param name="input">The mock input provider</param>
        /// <param name="manager">The input manager to update</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SimulateRightClick(MockInputProvider input, IInputManager manager, int x, int y)
        {
            // Set released state first
            input.SetMouseState(CreateReleasedMouseState(x, y));
            manager.Update();
            
            // Then set right button pressed
            input.SetMouseState(CreateMouseState(x, y, right: ButtonState.Pressed));
            manager.Update();
        }

        /// <summary>
        /// Simulates moving the mouse to a position without clicking.
        /// Useful for hover state tests.
        /// </summary>
        /// <param name="input">The mock input provider</param>
        /// <param name="manager">The input manager to update</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SimulateMouseMove(MockInputProvider input, IInputManager manager, int x, int y)
        {
            input.SetMouseState(CreateReleasedMouseState(x, y));
            manager.Update();
        }

        /// <summary>
        /// Creates a MouseState with all buttons released at the specified position.
        /// This is the most common mouse state configuration.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>MouseState with all buttons released</returns>
        public static MouseState CreateReleasedMouseState(int x, int y)
        {
            return new MouseState(
                x, y, 0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released
            );
        }

        /// <summary>
        /// Creates a MouseState with configurable button states.
        /// Reduces verbose constructor calls with sensible defaults.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="scrollWheel">Scroll wheel value (default: 0)</param>
        /// <param name="left">Left button state (default: Released)</param>
        /// <param name="middle">Middle button state (default: Released)</param>
        /// <param name="right">Right button state (default: Released)</param>
        /// <param name="xButton1">XButton1 state (default: Released)</param>
        /// <param name="xButton2">XButton2 state (default: Released)</param>
        /// <returns>Configured MouseState</returns>
        public static MouseState CreateMouseState(
            int x,
            int y,
            int scrollWheel = 0,
            ButtonState left = ButtonState.Released,
            ButtonState middle = ButtonState.Released,
            ButtonState right = ButtonState.Released,
            ButtonState xButton1 = ButtonState.Released,
            ButtonState xButton2 = ButtonState.Released)
        {
            return new MouseState(x, y, scrollWheel, left, middle, right, xButton1, xButton2);
        }
    }
}

