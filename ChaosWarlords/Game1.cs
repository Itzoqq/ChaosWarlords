using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords
{
    [ExcludeFromCodeCoverage]
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private StateManager _stateManager;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.IsFullScreen = true;
            _graphics.HardwareModeSwitch = false;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Initialize State Manager
            _stateManager = new StateManager(this);

            // Push the main gameplay state
            _stateManager.PushState(new GameplayState(this));
        }

        protected override void Update(GameTime gameTime)
        {
            // Delegate logic to current state
            _stateManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue);

            _spriteBatch.Begin();

            // Delegate drawing to current state
            _stateManager.Draw(_spriteBatch);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            GameLogger.Log("Session Ended. Flushing logs.", LogChannel.General);
            GameLogger.FlushToFile();
            base.UnloadContent();
        }
    }
}