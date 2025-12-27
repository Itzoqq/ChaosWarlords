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
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ChaosWarlords.Tests")]

namespace ChaosWarlords
{
    [ExcludeFromCodeCoverage]
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        public IStateManager StateManager { get; private set; }
        public IInputProvider InputProvider { get; private set; }
        public ICardDatabase CardDatabase { get; private set; }

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
            StateManager = new StateManager(this);

            // 1. Initialize Card Database Service (New Step)
            var cardDatabase = new CardDatabase();
            CardDatabase = cardDatabase;
            try
            {
                // In a real game, this file load might be wrapped in an IFileProvider or similar abstraction.
                // We're keeping it close to Monogame's TitleContainer for now.
                using (var stream = TitleContainer.OpenStream("Content/data/cards.json"))
                {
                    cardDatabase.Load(stream);
                }
            }
            catch (System.Exception ex)
            {
                // In a real game, you might show a fatal error screen here
                GameLogger.Log($"Failed to load card database: {ex.Message}", LogChannel.Error);
            }

            // 2. Create Input Service
            InputProvider = new MonoGameInputProvider();

            // 3. Inject BOTH into GameplayState
            // This line is correct because GameplayState implements IGameplayState, 
            // which implements IState, and PushState expects IState.
            // 3. Start with Main Menu (MVC Wiring)
            // Logic
            var buttonManager = new ChaosWarlords.Source.Rendering.UI.ButtonManager();
            
            // View
            var mainMenuView = new ChaosWarlords.Source.Rendering.Views.MainMenuView(GraphicsDevice, Content, buttonManager);

            // State (Controller)
            var mainMenuState = new MainMenuState(
                this, 
                InputProvider, 
                StateManager, 
                CardDatabase, 
                mainMenuView, 
                buttonManager
            );

            StateManager.PushState(mainMenuState);
        }

        protected override void Update(GameTime gameTime)
        {
            // Delegate logic to current state
            StateManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue);

            _spriteBatch.Begin();

            // Delegate drawing to current state
            StateManager.Draw(_spriteBatch);

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

