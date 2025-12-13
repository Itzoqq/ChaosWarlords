using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Systems
{
    public class InputManager
    {
        private readonly IInputProvider _provider;

        private KeyboardState _currentKeyboard;
        private KeyboardState _previousKeyboard;
        private MouseState _currentMouse;
        private MouseState _previousMouse;

        // Constructor Injection: We MUST have a provider to function.
        public InputManager(IInputProvider provider)
        {
            _provider = provider;
        }

        public Vector2 MousePosition => _currentMouse.Position.ToVector2();

        public void Update()
        {
            _previousKeyboard = _currentKeyboard;
            // Ask the provider for the state (could be real or fake)
            _currentKeyboard = _provider.GetKeyboardState();

            _previousMouse = _currentMouse;
            _currentMouse = _provider.GetMouseState();
        }

        // ... Rest of the methods (IsKeyJustPressed, etc.) remain exactly the same ...
        public bool IsKeyJustPressed(Keys key)
        {
            return _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
        }

        public bool IsKeyDown(Keys key)
        {
            return _currentKeyboard.IsKeyDown(key);
        }

        public bool IsLeftMouseJustClicked()
        {
            return _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        }

        public bool IsRightMouseJustClicked()
        {
            return _currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        }

        public bool IsMouseOver(Rectangle rect)
        {
            return rect.Contains(_currentMouse.Position);
        }

        public MouseState GetMouseState() => _currentMouse;
    }
}