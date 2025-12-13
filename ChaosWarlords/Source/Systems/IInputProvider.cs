using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Systems
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