using ChaosWarlords.Source.Core.Composition;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Rendering.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.GameStates
{
    /// <summary>Collects local match options before constructing gameplay and its immutable match context.</summary>
    public sealed class MatchSetupState : IState, IDrawableState
    {
        private readonly Game1 _game;
        private readonly IInputProvider _inputProvider;
        private readonly IStateManager _stateManager;
        private readonly ICardDatabase _cardDatabase;
        private readonly IReplayManager _replayManager;
        private readonly IGameLogger _logger;
        private readonly IButtonManager _buttonManager;
        private IMainMenuView? _view;
        private SimpleButton? _firstHalfDeckButton;
        private SimpleButton? _secondHalfDeckButton;
        private MouseState _previousMouseState;
        private bool _waitingForInitialRelease = true;

        public MarketDeckSelection MarketDeckSelection { get; private set; } = ChaosWarlords.Source.Core.Contexts.MarketDeckSelection.Default;

        public MatchSetupState(Game1 game, IInputProvider inputProvider, IStateManager stateManager,
            ICardDatabase cardDatabase, IReplayManager replayManager, IGameLogger logger,
            IMainMenuView? view = null, IButtonManager? buttonManager = null)
        {
            _game = game;
            _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _cardDatabase = cardDatabase ?? throw new ArgumentNullException(nameof(cardDatabase));
            _replayManager = replayManager ?? throw new ArgumentNullException(nameof(replayManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _view = view;
            _buttonManager = buttonManager ?? new ButtonManager();
        }

        public void LoadContent()
        {
            var viewport = _game?.GraphicsDevice?.Viewport ?? new Viewport(0, 0, 800, 600);
            const int buttonWidth = 260;
            const int buttonHeight = 50;
            int centerX = viewport.Width / 2 - buttonWidth / 2;
            int centerY = viewport.Height / 2;

            _firstHalfDeckButton = new SimpleButton(new Rectangle(centerX, centerY - 140, buttonWidth, buttonHeight), $"First half-deck: {MarketDeckSelection.First}", CycleFirstHalfDeck);
            _secondHalfDeckButton = new SimpleButton(new Rectangle(centerX, centerY - 70, buttonWidth, buttonHeight), $"Second half-deck: {MarketDeckSelection.Second}", CycleSecondHalfDeck);
            _buttonManager.AddButton(_firstHalfDeckButton);
            _buttonManager.AddButton(_secondHalfDeckButton);
            _buttonManager.AddButton(new SimpleButton(new Rectangle(centerX, centerY, buttonWidth, buttonHeight), "Start Match", StartMatch));
            _buttonManager.AddButton(new SimpleButton(new Rectangle(centerX, centerY + 70, buttonWidth, buttonHeight), "Back", Back));

            if (_view is null && _game?.GraphicsDevice is not null)
            {
                _view = new MainMenuView(_game.GraphicsDevice, _game.Content, _buttonManager, _logger);
            }

            _view?.LoadContent();
            _previousMouseState = _inputProvider.GetMouseState();
        }

        public void UnloadContent()
        {
            _buttonManager.Clear();
            _view?.UnloadContent();
        }

        public void Update(GameTime gameTime)
        {
            var currentMouse = _inputProvider.GetMouseState();
            if (WaitForInitialRelease(currentMouse, gameTime)) return;

            UpdateButtons(currentMouse);
            _view?.Update(gameTime);
        }

        private bool WaitForInitialRelease(MouseState currentMouse, GameTime gameTime)
        {
            if (!_waitingForInitialRelease) return false;

            _previousMouseState = currentMouse;
            if (currentMouse.LeftButton == ButtonState.Released) _waitingForInitialRelease = false;
            _view?.Update(gameTime);
            return true;
        }

        private void UpdateButtons(MouseState currentMouse)
        {
            bool isClick = currentMouse.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            _buttonManager.Update(currentMouse.Position, isClick);
            _previousMouseState = currentMouse;
        }

        public void Draw(SpriteBatch spriteBatch) => _view?.Draw(spriteBatch);

        private void CycleFirstHalfDeck()
        {
            MarketDeckSelection = MarketDeckSelection.WithFirst(MarketDeckSelection.NextDistinct(MarketDeckSelection.First, MarketDeckSelection.Second));
            _firstHalfDeckButton?.SetText($"First half-deck: {MarketDeckSelection.First}");
        }

        private void CycleSecondHalfDeck()
        {
            MarketDeckSelection = MarketDeckSelection.WithSecond(MarketDeckSelection.NextDistinct(MarketDeckSelection.Second, MarketDeckSelection.First));
            _secondHalfDeckButton?.SetText($"Second half-deck: {MarketDeckSelection.Second}");
        }

        private void StartMatch()
        {
            IGameplayView? gameplayView = null;
            int width = 1920;
            int height = 1080;
            if (_game?.GraphicsDevice is not null)
            {
                gameplayView = new GameplayView(_game.GraphicsDevice, _logger);
                width = _game.GraphicsDevice.Viewport.Width;
                height = _game.GraphicsDevice.Viewport.Height;
            }

            var dependencies = new GameDependencies
            {
                Game = _game,
                InputManager = new InputManager(_inputProvider),
                CardDatabase = _cardDatabase,
                Logger = _logger,
                UIManager = new UIManager(width, height, _logger),
                ReplayManager = _replayManager,
                MarketDeckSelection = MarketDeckSelection,
                View = gameplayView,
                ViewportWidth = width,
                ViewportHeight = height
            };
            _stateManager.ChangeState(new GameplayState(dependencies));
        }

        private void Back() => _stateManager.ChangeState(new MainMenuState(_game, _inputProvider, _stateManager, _cardDatabase, _replayManager, _logger));
    }
}
