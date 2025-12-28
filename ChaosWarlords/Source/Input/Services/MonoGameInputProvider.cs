using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
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

