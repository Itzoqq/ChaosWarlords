using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Interfaces.Services;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ChaosWarlords.Tests")]

namespace ChaosWarlords
{
    [ExcludeFromCodeCoverage]
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        public IStateManager StateManager { get; private set; } = null!;
        public IInputProvider InputProvider { get; private set; } = null!;
        public ICardDatabase CardDatabase { get; private set; } = null!;
        public IReplayManager ReplayManager { get; private set; } = null!;
        public IGameLogger Logger { get; }

        public Game1(IGameLogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                Logger.Log($"Failed to load card database: {ex.Message}", LogChannel.Error);
            }

            // 1.5 Initialize ReplayManager
            ReplayManager = new ReplayManager(Logger);

            // 2. Create Input Service and UIManager (Composition Root)
            InputProvider = new MonoGameInputProvider();
            var inputManager = new ChaosWarlords.Source.Managers.InputManager(InputProvider); // Full qualification or ensure using
            
            var viewportWidth = GraphicsDevice.Viewport.Width;
            var viewportHeight = GraphicsDevice.Viewport.Height;
            var uiManager = new UIManager(viewportWidth, viewportHeight, Logger);

            // Restore UI Elements
            var buttonManager = new ChaosWarlords.Source.Rendering.UI.ButtonManager();
            var mainMenuView = new ChaosWarlords.Source.Rendering.Views.MainMenuView(GraphicsDevice, Content, buttonManager, Logger);




            // State (Controller)
            var mainMenuState = new MainMenuState(
                this,
                InputProvider,
                StateManager,
                CardDatabase,
                ReplayManager,
                Logger,
                mainMenuView,
                buttonManager
            );
            
            // We need to instantiate GameplayState differently if it is used here, 
            // but Game1 only pushes MainMenuState initially.
            // If GameplayState is created elsewhere, it must use the new signature.
            // However, Game1 usually doesn't create GameplayState directly here.
            
            // Wait, looking at the previous code, Game1 was NOT instantiating GameplayState in LoadContent.
            // It was pushing MainMenuState. 
            // So where is GameplayState instantiated? 
            // Usually MainMenuState creates it when "Start Game" is clicked.
            
            // Checking MainMenuState...
            
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
            Logger.Log("Session Ended. Flushing logs.", LogChannel.General);
            base.UnloadContent();
        }
    }
}

