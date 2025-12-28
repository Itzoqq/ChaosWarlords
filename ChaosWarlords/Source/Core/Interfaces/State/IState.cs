using Microsoft.Xna.Framework;


namespace ChaosWarlords.Source.Core.Interfaces.State
{
    /// <summary>
    /// Base interface for all game states (e.g., Main Menu, Gameplay, Pause).
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Loads necessary content (textures, fonts, audio) for this state.
        /// Called when the state is entered.
        /// </summary>
        void LoadContent();

        /// <summary>
        /// Unloads content and performs cleanup.
        /// Called when the state is exited.
        /// </summary>
        void UnloadContent();

        /// <summary>
        /// Updates logic for a single frame.
        /// </summary>
        /// <param name="gameTime">Snapshot of timing values.</param>
        void Update(GameTime gameTime);
    }
}



