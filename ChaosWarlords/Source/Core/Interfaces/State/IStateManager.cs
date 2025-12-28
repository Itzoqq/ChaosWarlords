using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.Core.Interfaces.State
{
    /// <summary>
    /// Manages the game state stack.
    /// </summary>
    public interface IStateManager
    {
        /// <summary>
        /// Pushes a new state onto the stack.
        /// </summary>
        void PushState(IState state);

        /// <summary>
        /// Pops the current state from the stack.
        /// </summary>
        void PopState();

        /// <summary>
        /// Changes the current state (pops current, pushes new).
        /// </summary>
        void ChangeState(IState state);

        /// <summary>
        /// Gets the current active state.
        /// </summary>
        IState GetCurrentState();

        /// <summary>
        /// Updates the current state.
        /// </summary>
        void Update(GameTime gameTime);

        /// <summary>
        /// Draws the current state.
        /// </summary>
        void Draw(SpriteBatch spriteBatch);
    }
}



