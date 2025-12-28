using ChaosWarlords.Source.Core.Interfaces.Input;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Input
{
    [ExcludeFromCodeCoverage]
    public class MonoGameInputProvider : IInputProvider
    {
        public MouseState GetMouseState() => Mouse.GetState();
        public KeyboardState GetKeyboardState() => Keyboard.GetState();
    }
}

