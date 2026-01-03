using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Rendering.Views;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.GameStates
{
    public class VictoryState : IState, IDrawableState, System.IDisposable
    {
        private readonly Game1 _game;
        private readonly IInputProvider _inputProvider;
        private readonly IStateManager _stateManager;
        private readonly IGameLogger _logger;
        private readonly VictoryDto _victoryData;
        private readonly IVictoryView _view;
        private readonly ButtonManager _buttonManager;

        // Main Constructor
        public VictoryState(Game1 game, VictoryDto victoryData) 
            : this(game, victoryData, game.InputProvider, game.StateManager, game.Logger, null)
        { }

        // Testing / DI Constructor
        public VictoryState(Game1 game, VictoryDto victoryData, IInputProvider inputProvider, IStateManager stateManager, IGameLogger logger, IVictoryView? view)
        {
            _game = game; // Can be null in tests if careful, but logic uses it.
            _victoryData = victoryData ?? throw new System.ArgumentNullException(nameof(victoryData));
            _inputProvider = inputProvider ?? throw new System.ArgumentNullException(nameof(inputProvider));
            _stateManager = stateManager ?? throw new System.ArgumentNullException(nameof(stateManager));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));

            _buttonManager = new ButtonManager();

            // Initialize View if not provided
            if (view == null)
            {
                 // Production Path
                 if (game == null) throw new ArgumentNullException(nameof(game), "Game cannot be null when creating default view.");
                 _view = new VictoryView(game.GraphicsDevice, game.Content, _buttonManager, victoryData, _logger);
            }
            else
            {
                // Testing Path
                _view = view;
            }

            SetupButtons();
        }

        private void SetupButtons()
        {
            // Main Menu Button
            _buttonManager.AddButton(new SimpleButton(
                _view.MainMenuButtonRect,
                "Main Menu",
                () => ReturnToMainMenu()
            ));
        }

        private void ReturnToMainMenu()
        {
             _logger.Log("Returning to Main Menu from Victory Screen.", LogChannel.Info);
            
            // We want to reset the game state.
            // Create a fresh MainMenuState.
            // DEPENDENCY: MainMenuState constructor requires many services.
            var mainMenu = new MainMenuState(_game);
            
            // Replace current state (Victory) with Main Menu
            _stateManager.ChangeState(mainMenu);
        }

        public void LoadContent()
        {
            // View loaded in constructor or here?
            // StateManager calls LoadContent when pushing.
            // But we created view in constructor which loaded content.
            // That's fine for now, but strictly should be here.
        }

        public void UnloadContent()
        {
            _view?.Dispose();
        }

        private Microsoft.Xna.Framework.Input.MouseState _previousMouseState;

        public void Update(GameTime gameTime)
        {
            var currentMouse = _inputProvider.GetMouseState();
            var mousePos = currentMouse.Position;
            bool isClick = false;

            // Only register click on Release
            if (currentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && 
                _previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                isClick = true;
            }

            // Update Buttons
            _buttonManager.Update(mousePos, isClick);
            
            // Update View hover states
            _view.IsMainMenuHovered = _view.MainMenuButtonRect.Contains(mousePos);

            _previousMouseState = currentMouse;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            _view.Draw(spriteBatch);
        }
        public void Dispose()
        {
            _view?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
