using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.States;

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



