using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Core.Interfaces;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Interfaces;

namespace ChaosWarlords.Source.States
{
    public class MainMenuState : IState
    {
        private Game1 _game;
        private Texture2D _background;
        private SpriteFont _font;
        
        // Dependencies
        private readonly IInputProvider _inputProvider;
        private readonly IStateManager _stateManager;
        private readonly ICardDatabase _cardDatabase;
        private IButtonManager _buttonManager;

        // Input state just for click detection
        private MouseState _previousMouseState;

        // For testing/DI support
        // We still keep Game1 for Content loading (Service Locator pattern for assets is common in MonoGame)
        // Check if Game1 exposes StateManager/CardDatabase publicly? Yes.
        public MainMenuState(Game1 game) : this(
            game, 
            game.InputProvider, 
            game.StateManager, 
            game.CardDatabase, 
            null) { }

        public MainMenuState(
            Game1 game, 
            IInputProvider inputProvider, 
            IStateManager stateManager,
            ICardDatabase cardDatabase,
            IButtonManager buttonManager = null)
        {
            _game = game;
            _inputProvider = inputProvider ?? throw new System.ArgumentNullException(nameof(inputProvider));
            _stateManager = stateManager; // Can be null if game not fully init? No, should be required.
            _cardDatabase = cardDatabase;
            _buttonManager = buttonManager;
        }

        public void LoadContent()
        {
            // Try to load background
            try 
            {
                _background = _game.Content.Load<Texture2D>("Textures/Backgrounds/MainMenuBG");
            }
            catch 
            {
                if (_game.GraphicsDevice != null)
                {
                    _background = new Texture2D(_game.GraphicsDevice, 1, 1);
                    _background.SetData(new Color[] { Color.DarkSlateGray });
                }
                GameLogger.Log("MainMenuBG not found, using placeholder.", LogChannel.Warning);
            }

            // Load font
            try
            {
                _font = _game.Content.Load<SpriteFont>("fonts/DefaultFont");
            }
            catch
            {
                // Fallback handled by ButtonManager not crashing if font is null? 
                // We'll pass it anyway.
            }

            SetupButtons();
        }

        private void SetupButtons()
        {
            // Initialize ButtonManager if not injected
            if (_buttonManager == null)
            {
                 _buttonManager = new ButtonManager(_game.GraphicsDevice, _font);
            }

            // Safe viewport access for testing where GraphicsDevice might be null
            var viewport = (_game.GraphicsDevice != null) ? _game.GraphicsDevice.Viewport : new Viewport(0, 0, 800, 600);
            
            int buttonWidth = 200;
            int buttonHeight = 50;
            int centerX = viewport.Width / 2 - buttonWidth / 2;
            int centerY = viewport.Height / 2;

            var startBtnRect = new Rectangle(centerX, centerY, buttonWidth, buttonHeight);
            var exitBtnRect = new Rectangle(centerX, centerY + 70, buttonWidth, buttonHeight);

            _buttonManager.AddButton(new SimpleButton(
                startBtnRect, 
                "Start Game", 
                () => StartGame()
            ));

            _buttonManager.AddButton(new SimpleButton(
                exitBtnRect, 
                "Exit", 
                () => _game.Exit()
            ));
        }

        public void UnloadContent()
        {
            // Clean up
             _buttonManager?.Clear();
        }

        public void Update(GameTime gameTime)
        {
            MouseState currentMouse = _inputProvider.GetMouseState();
            Point mousePos = currentMouse.Position;
            
            bool isClick = currentMouse.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;

            _buttonManager?.Update(mousePos, isClick);

            _previousMouseState = currentMouse;
        }

        private void StartGame()
        {
            if (_cardDatabase == null || _inputProvider == null || _stateManager == null)
            {
                GameLogger.Log("Cannot start game: Services not initialized.", LogChannel.Error);
                return;
            }

            _stateManager.ChangeState(new GameplayState(_game, _inputProvider, _cardDatabase));
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var viewport = _game.GraphicsDevice.Viewport;

            if (_background != null)
            {
                spriteBatch.Draw(_background, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
            }

            _buttonManager?.Draw(spriteBatch);
        }
    }
}
