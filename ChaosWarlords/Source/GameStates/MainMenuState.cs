using ChaosWarlords.Source.Rendering.ViewModels;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Rendering.UI;

using ChaosWarlords.Source.Rendering.Views;

namespace ChaosWarlords.Source.States
{
    public class MainMenuState : IState, IDrawableState
    {
        private Game1 _game;

        
        // Dependencies
        private readonly IInputProvider _inputProvider;
        private readonly IStateManager _stateManager;
        private readonly ICardDatabase _cardDatabase;
        private readonly IMainMenuView _view; // Can be null for Headless Server
        private IButtonManager _buttonManager;

        // Input state just for click detection
        private MouseState _previousMouseState;
        private bool _waitingForInitialRelease = true; // Wait for button release before accepting input

        // For testing/DI support
        // We still keep Game1 for Content loading (Service Locator pattern for assets is common in MonoGame)
        // Check if Game1 exposes StateManager/CardDatabase publicly? Yes.
        public MainMenuState(Game1 game) : this(
            game, 
            game.InputProvider, 
            game.StateManager, 
            game.CardDatabase, 
            null,
            null) { }

        public MainMenuState(
            Game1 game, 
            IInputProvider inputProvider, 
            IStateManager stateManager,
            ICardDatabase cardDatabase,
            IMainMenuView view = null,
            IButtonManager buttonManager = null)
        {
            _game = game;
            _inputProvider = inputProvider ?? throw new System.ArgumentNullException(nameof(inputProvider));
            _stateManager = stateManager;
            _cardDatabase = cardDatabase;
            _view = view;
            _buttonManager = buttonManager;
        }

        public void LoadContent()
        {
             // Logic Setup
            if (_buttonManager == null)
            {
               _buttonManager = new ButtonManager(); // Logic Only
            }

            SetupButtons();

            // View Setup (Only if View exists - Client Side)
            _view?.LoadContent();

            // Initialize mouse state
            _previousMouseState = _inputProvider.GetMouseState();
        }

        private void SetupButtons()
        {
            // Safe viewport access for testing where GraphicsDevice might be null
            var viewport = (_game?.GraphicsDevice != null) ? _game.GraphicsDevice.Viewport : new Viewport(0, 0, 800, 600);
            
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
             _view?.UnloadContent();
        }

        public void Update(GameTime gameTime)
        {
            MouseState currentMouse = _inputProvider.GetMouseState();
            Point mousePos = currentMouse.Position;
            
            // ROBUST FIX: Wait for Initial Release
            // If the user enters this state with the mouse button held down (from clicking the menu button),
            // we MUST wait until they release it before we start tracking clicks.
            // This prevents "drag-through" clicks or immediate triggering.
            if (_waitingForInitialRelease)
            {
                if (currentMouse.LeftButton == ButtonState.Released)
                {
                    _waitingForInitialRelease = false;
                    _previousMouseState = currentMouse;
                }
                else
                {
                    // Still holding button, ignore and update previous state to current
                    // so we don't trigger a 'click' the moment we unlock
                    _previousMouseState = currentMouse; 
                    _view?.Update(gameTime);
                    return; 
                }
            }
            
            // Industry Standard: Require FRESH clicks only
            // A click is only valid if BOTH the press AND release happen in this state
            // This prevents clicks from previous states from bleeding through
            bool isClick = false;
            
            // Only detect click if we saw the button pressed in a previous frame
            if (currentMouse.LeftButton == ButtonState.Released && 
                _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                isClick = true;
            }

            _buttonManager?.Update(mousePos, isClick);
            _view?.Update(gameTime);

            _previousMouseState = currentMouse;
        }

        private void StartGame()
        {
            if (_cardDatabase == null || _inputProvider == null || _stateManager == null)
            {
                GameLogger.Log("Cannot start game: Services not initialized.", LogChannel.Error);
                return;
            }

            // Create View (if GraphicsDevice is available - Client Mode)
            IGameplayView gameplayView = null;
            int width = 1920;
            int height = 1080;

            if (_game?.GraphicsDevice != null)
            {
                gameplayView = new GameplayView(_game.GraphicsDevice);
                width = _game.GraphicsDevice.Viewport.Width;
                height = _game.GraphicsDevice.Viewport.Height;
            }

            _stateManager.ChangeState(new GameplayState(_game, _inputProvider, _cardDatabase, gameplayView, width, height));
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Delegate to the View
            // If _view is null (Server/Headless), we simply don't draw.
            // This decouples the State from the GraphicsDevice.
            _view?.Draw(spriteBatch);
        }
    }
}


