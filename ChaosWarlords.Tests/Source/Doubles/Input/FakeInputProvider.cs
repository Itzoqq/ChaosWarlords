using ChaosWarlords.Source.Core.Interfaces.Input;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Tests.Source.Doubles.Input
{
    /// <summary>
    /// Controllable stand-in for the literal OS/hardware input boundary - the one thing
    /// InputPipelineScenario legitimately fakes (nothing can move a real mouse in a test
    /// process). Everything downstream of this (InputManager, GameplayInputCoordinator,
    /// PlayerController, UIManager) is real - see InputPipelineScenario's own doc comment.
    /// </summary>
    public class FakeInputProvider : IInputProvider
    {
        private MouseState _mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        private KeyboardState _keyboard = new();

        public void SetMouse(int x, int y, bool leftDown = false, bool rightDown = false)
        {
            _mouse = new MouseState(
                x, y, 0,
                leftDown ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released,
                rightDown ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        public void SetKey(Keys key, bool down)
        {
            _keyboard = down ? new KeyboardState(key) : new KeyboardState();
        }

        public MouseState GetMouseState() => _mouse;
        public KeyboardState GetKeyboardState() => _keyboard;
    }
}
