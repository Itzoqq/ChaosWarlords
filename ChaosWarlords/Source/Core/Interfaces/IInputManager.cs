using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Interfaces
{
    /// <summary>
    /// Provides input state management and query methods.
    /// </summary>
    public interface IInputManager
    {
        /// <summary>
        /// Gets the current mouse position.
        /// </summary>
        Vector2 MousePosition { get; }

        /// <summary>
        /// Updates the input state. Should be called once per frame.
        /// </summary>
        void Update();

        /// <summary>
        /// Checks if a key was just pressed this frame.
        /// </summary>
        bool IsKeyJustPressed(Keys key);

        /// <summary>
        /// Checks if a key is currently held down.
        /// </summary>
        bool IsKeyDown(Keys key);

        /// <summary>
        /// Checks if the left mouse button was just clicked this frame.
        /// </summary>
        bool IsLeftMouseJustClicked();

        /// <summary>
        /// Checks if the right mouse button was just clicked this frame.
        /// </summary>
        bool IsRightMouseJustClicked();

        /// <summary>
        /// Checks if the mouse is currently over a rectangle.
        /// </summary>
        bool IsMouseOver(Rectangle rect);

        /// <summary>
        /// Gets the current mouse state.
        /// </summary>
        MouseState GetMouseState();
    }
}
