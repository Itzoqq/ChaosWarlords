using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.Core.Interfaces.State
{
    /// <summary>
    /// Represents a game state that has a visual component to render.
    /// </summary>
    public interface IDrawableState
    {
        /// <summary>
        /// Renders the state to the screen.
        /// </summary>
        /// <param name="spriteBatch">The active SpriteBatch used for 2D rendering.</param>
        void Draw(SpriteBatch spriteBatch);
    }
}
