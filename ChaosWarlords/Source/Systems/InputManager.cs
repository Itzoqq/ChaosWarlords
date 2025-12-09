using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Systems
{
    public class InputManager
    {
        private KeyboardState _currentKeyboard;
        private KeyboardState _previousKeyboard;
        private MouseState _currentMouse;
        private MouseState _previousMouse;

        public Vector2 MousePosition => _currentMouse.Position.ToVector2();

        public void Update()
        {
            _previousKeyboard = _currentKeyboard;
            _currentKeyboard = Keyboard.GetState();

            _previousMouse = _currentMouse;
            _currentMouse = Mouse.GetState();
        }

        // Returns true ONLY on the frame the key is first pressed
        public bool IsKeyJustPressed(Keys key)
        {
            return _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
        }

        public bool IsKeyDown(Keys key)
        {
            return _currentKeyboard.IsKeyDown(key);
        }

        // Returns true ONLY on the frame the button is first clicked
        public bool IsLeftMouseJustClicked()
        {
            return _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        }

        public bool IsRightMouseJustClicked()
        {
            return _currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        }

        // Helper to check if mouse is inside a rectangle
        public bool IsMouseOver(Rectangle rect)
        {
            return rect.Contains(_currentMouse.Position);
        }

        public MouseState GetMouseState() => _currentMouse;
    }
}