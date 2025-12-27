using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Core.Interfaces.Input
{
    /// <summary>
    /// Abstraction layer for Input. 
    /// Allows swapping between Real Hardware (MonoGame) and Mock Data (Unit Tests).
    /// </summary>
    public interface IInputProvider
    {
        MouseState GetMouseState();
        KeyboardState GetKeyboardState();
    }
}



