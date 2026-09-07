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
        // Concrete type, not ICrashReporter: private, never reassigned/mocked (Game1 is the
        // composition root and is excluded from coverage, same as its other `new`-constructed
        // dependencies below) - CA1859 flags the interface as unnecessary indirection here.
        private readonly CrashReporter _crashReporter;

        public Game1(IGameLogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _crashReporter = new CrashReporter(logger);
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

            // 1. Initialize Localization Service (card Name/Description live here, not in
            // cards.json - see CardDatabase's CardData doc comment). Loaded BEFORE
            // CardDatabase since CardFactory.CreateFromData needs it at card-creation time.
            var localizationService = new LocalizationManager(Logger);
            try
            {
                using (var stream = TitleContainer.OpenStream("Content/data/localization/en_US.json"))
                {
                    localizationService.Load(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load localization bundle: {ex.Message}", LogChannel.Error);
            }

            // 2. Initialize Card Database Service
            var cardDatabase = new CardDatabase(localizationService, Logger);
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
            catch (Exception ex)
            {
                // Fail fast rather than limp forward with an empty card database: nothing
                // downstream (MatchFactory.Build() and beyond) guards against an empty
                // CardDatabase, so continuing here would only defer this exact failure to a
                // completely unrelated-looking crash deep inside match setup, several log lines
                // away from the real cause.
                Logger.Log($"Failed to load card database - cannot continue: {ex.Message}", LogChannel.Error);
                throw;
            }

            // 1.5 Initialize ReplayManager
            ReplayManager = new ReplayManager(Logger);

            // 2. Create Input Service and UIManager (Composition Root)
            InputProvider = new MonoGameInputProvider();
            var inputManager = new InputManager(InputProvider); // Full qualification or ensure using

            var viewportWidth = GraphicsDevice.Viewport.Width;
            var viewportHeight = GraphicsDevice.Viewport.Height;
            var uiManager = new UIManager(viewportWidth, viewportHeight, Logger);

            // Restore UI Elements
            var buttonManager = new Source.Rendering.UI.ButtonManager();
            var mainMenuView = new Source.Rendering.Views.MainMenuView(GraphicsDevice, Content, buttonManager, Logger);




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
            // Per-frame crash isolation: without this, ANY uncaught exception anywhere in game
            // logic (a bad card effect, a bug in a future AI policy, etc.) propagates all the
            // way up through MonoGame's own loop to Program.cs's one top-level catch, which
            // logs and kills the whole process - "log it, then die" was the only line of
            // defense in the app. Catching here instead logs verbosely, dumps a crash-repro
            // replay (see CrashReporter), and skips just this frame's update, letting the
            // session keep running. See planning.txt's crash-safety audit.
            try
            {
                // Delegate logic to current state
                StateManager.Update(gameTime);
            }
            catch (Exception ex)
            {
                _crashReporter.ReportCrash(ex, ReplayManager, "Update");
            }
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue);

            _spriteBatch.Begin();

            try
            {
                // Delegate drawing to current state
                StateManager.Draw(_spriteBatch);
            }
            catch (Exception ex)
            {
                _crashReporter.ReportCrash(ex, ReplayManager, "Draw");
            }

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

