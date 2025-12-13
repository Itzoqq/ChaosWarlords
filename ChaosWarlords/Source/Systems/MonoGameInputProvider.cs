using Microsoft.Xna.Framework.Input;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Systems
{
    [ExcludeFromCodeCoverage] // We can't unit test hardware calls, so we exclude this wrapper.
    public class MonoGameInputProvider : IInputProvider
    {
        public MouseState GetMouseState() => Mouse.GetState();
        public KeyboardState GetKeyboardState() => Keyboard.GetState();
    }
}