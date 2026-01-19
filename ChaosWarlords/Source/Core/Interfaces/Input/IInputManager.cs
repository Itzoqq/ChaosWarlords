using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Core.Events;

namespace ChaosWarlords.Source.Core.Interfaces.Input
{
    /// <summary>
    /// Provides input state management and query methods.
    /// </summary>
    public interface IInputManager
    {
        // New Event-Driven API
        event EventHandler<InputEventArgs> OnInputEvent;

        /// <summary>
        /// Gets the current mouse position.
        /// </summary>
        Vector2 MousePosition { get; }

        /// <summary>
        /// Updates the input state. Should be called once per frame.
        /// </summary>
        void Update();

        /// <summary>
        /// Checks if the mouse is currently over a rectangle.
        /// </summary>
        bool IsMouseOver(Rectangle rect);
    }
}



