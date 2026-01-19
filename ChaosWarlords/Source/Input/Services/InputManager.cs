using System;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Events; // Explicitly adding this too just in case
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Managers
{
    public class InputManager : IInputManager
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

        public event EventHandler<InputEventArgs>? OnInputEvent;

        public Vector2 MousePosition => _currentMouse.Position.ToVector2();

        public void Update()
        {
            _previousKeyboard = _currentKeyboard;
            _currentKeyboard = _provider.GetKeyboardState();

            _previousMouse = _currentMouse;
            _currentMouse = _provider.GetMouseState();

            // Detect and Fire Events
            FireMouseEvents();
            FireKeyboardEvents();
        }

        private void FireMouseEvents()
        {
            // Left Click
            if (_currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
            {
                OnInputEvent?.Invoke(this, new InputEventArgs(InputEventType.LeftClick, MousePosition));
            }

            // Right Click
            if (_currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
            {
                OnInputEvent?.Invoke(this, new InputEventArgs(InputEventType.RightClick, MousePosition));
            }
        }

        private void FireKeyboardEvents()
        {
            // Optimization: Only iterate if keys are pressed
            var pressedKeys = _currentKeyboard.GetPressedKeys();
            if (pressedKeys.Length == 0 && _previousKeyboard.GetPressedKeys().Length == 0) return;

            foreach (var key in pressedKeys)
            {
                if (!_previousKeyboard.IsKeyDown(key))
                {
                    OnInputEvent?.Invoke(this, new InputEventArgs(InputEventType.KeyDown, MousePosition, key));
                }
            }
        }

        public bool IsKeyDown(Keys key)
        {
            return _currentKeyboard.IsKeyDown(key);
        }

        public bool IsMouseOver(Rectangle rect)
        {
            return rect.Contains(_currentMouse.Position);
        }

        public MouseState GetMouseState() => _currentMouse;
    }
}

