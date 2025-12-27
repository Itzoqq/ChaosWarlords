using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Rendering.Views;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.States.Input;

using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Managers;
using System;
using System.Linq;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IGameplayState
    {
        private readonly Game _game;
        private readonly IInputProvider _inputProvider;
        private readonly ICardDatabase _cardDatabase;

        internal IGameplayView _view;
        internal IMatchManager _matchManager;
        internal MatchContext _matchContext;
        internal InputManager _inputManagerBacking;
        internal IUIManager _uiManagerBacking;
        internal bool _isMarketOpenBacking = false;

        // New Coordinators
        private GameplayInputCoordinator _inputCoordinator;
        private InteractionMapper _interactionMapper;
        private CardPlaySystem _cardPlaySystem;
        private PlayerController _playerController;
        private UIEventMediator _uiEventMediator;

        public IInputManager InputManager => _inputManagerBacking;
        public IUIManager UIManager => _uiManagerBacking;
        public IMatchManager MatchManager => _matchManager;

        public IMapManager MapManager => _matchContext?.MapManager;
        public IMarketManager MarketManager => _matchContext?.MarketManager;
        public IActionSystem ActionSystem => _matchContext?.ActionSystem;
        public ITurnManager TurnManager => _matchContext?.TurnManager;
        public MatchContext MatchContext => _matchContext;

        public IInputMode InputMode => _inputCoordinator.CurrentMode;

        public int HandY => _view?.HandY ?? 0;
        public int PlayedY => _view?.PlayedY ?? 0;

        public bool IsMarketOpen
        {
            get => _isMarketOpenBacking;
            set
            {
                _isMarketOpenBacking = value;
                _inputCoordinator.SetMarketMode(value);
            }
        }

        // Expose UIEventMediator state for tests and views
        public bool IsConfirmationPopupOpen => _uiEventMediator?.IsConfirmationPopupOpen ?? false;
        public bool IsPauseMenuOpen => _uiEventMediator?.IsPauseMenuOpen ?? false;

        public GameplayState(Game game, IInputProvider inputProvider, ICardDatabase cardDatabase, IGameplayView view = null)
        {
            _game = game;
            _inputProvider = inputProvider;
            _cardDatabase = cardDatabase;
            _view = view;
        }

        public void LoadContent()
        {
            if (_game == null) return;
            
            GameLogger.Initialize();

            InitializeInfrastructure();
            InitializeView();
            InitializeMatch();
            InitializeSystems();
        }

        private void InitializeInfrastructure()
        {
            _inputManagerBacking = new InputManager(_inputProvider);
            var graphicsDevice = _game.GraphicsDevice;
            _uiManagerBacking = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
        }

        private void InitializeView()
        {
            if (_view != null)
            {
                 // Content loading is managed by State lifecycle
                 if (_game != null) _view.LoadContent(_game.Content);
                 _interactionMapper = new InteractionMapper(_view);
            }
        }

        private void InitializeMatch()
        {
            var builder = new MatchFactory(_cardDatabase);
            var worldData = builder.Build();

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase
            );

            _matchManager = new MatchManager(_matchContext);

            // Don't draw cards during Setup phase
            if (_matchContext.CurrentPhase != MatchPhase.Setup && _matchContext.TurnManager.Players != null)
            {
                foreach (var player in _matchContext.TurnManager.Players)
                {
                    player.DrawCards(5);
                }
            }

            _matchContext.MapManager.CenterMap(_game.GraphicsDevice.Viewport.Width, _game.GraphicsDevice.Viewport.Height);

            // Subscribe to Setup auto-advance
            _matchContext.MapManager.OnSetupDeploymentComplete += HandleSetupDeploymentComplete;
        }

        private void InitializeSystems()
        {
            _inputCoordinator = new GameplayInputCoordinator(this, _inputManagerBacking, _matchContext);
            _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, () => SwitchToTargetingMode());
            
            _uiEventMediator = new UIEventMediator(this, _uiManagerBacking, _matchContext.ActionSystem, _game as Game1);
            _uiEventMediator.Initialize();

            _playerController = new PlayerController(this, _inputManagerBacking, _inputCoordinator, _interactionMapper);
        }

        private void HandleSetupDeploymentComplete()
        {
            if (_matchContext.CurrentPhase == MatchPhase.Setup)
            {
                _matchManager.EndTurn();
            }
        }

        public void UnloadContent()
        {
            _uiEventMediator?.Cleanup();
        }

        public void Update(GameTime gameTime)
        {
            _inputManagerBacking.Update();
            
            // Sync UI state via mediator
            _uiEventMediator.Update();
            _uiManagerBacking.Update(_inputManagerBacking);

            if (_uiEventMediator.IsConfirmationPopupOpen) return; // Block other input if popup is open

            // Delegate all input handling to PlayerController
            _playerController.Update();

            _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_game == null) return;

            string targetingText = "";
            if (_matchContext.ActionSystem.IsTargeting())
            {
                targetingText = GetTargetingText(_matchContext.ActionSystem.CurrentState);
            }

            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, (UIManager)_uiManagerBacking, IsMarketOpen, targetingText, _uiEventMediator.IsConfirmationPopupOpen, _uiEventMediator.IsPauseMenuOpen);

            // Phase 0 UI Overlay
            if (_matchContext.CurrentPhase == MatchPhase.Setup)
            {
                _view?.DrawSetupPhaseOverlay(spriteBatch, _matchContext.TurnManager.ActivePlayer);
            }
        }

        public void PlayCard(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            _cardPlaySystem.PlayCard(card);
        }

        public bool HasViableTargets(Card card) => _cardPlaySystem.HasViableTargets(card);



        public void MoveCardToPlayed(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            _matchManager.MoveCardToPlayed(card);
        }

        public bool CanEndTurn(out string reason) => _matchManager.CanEndTurn(out reason);

        public void EndTurn()
        {
            if (_matchContext.ActionSystem.IsTargeting()) _matchContext.ActionSystem.CancelTargeting();
            _matchManager.EndTurn();
            SwitchToNormalMode();
        }

        public void ToggleMarket() { IsMarketOpen = !IsMarketOpen; }
        public void CloseMarket() { IsMarketOpen = false; }

        public void SwitchToTargetingMode() => _inputCoordinator.SwitchToTargetingMode();
        public void SwitchToNormalMode() => _inputCoordinator.SwitchToNormalMode();

        // --- FIX IS HERE ---
        public void SwitchToPromoteMode(int amount)
        {
            if (_matchContext == null) throw new InvalidOperationException("Match context not initialized");
            if (_matchContext.ActionSystem == null) throw new InvalidOperationException("Action system not initialized");

            // We must set the ActionSystem state explicitly so the InputCoordinator knows 
            // to instantiate the PromoteInputMode instead of the generic TargetingInputMode.
            // We pass 'null' for the card because Promotion is a turn-phase action, not a card-specific action.
            _matchContext.ActionSystem.StartTargeting(ActionState.SelectingCardToPromote, null);

            _inputCoordinator.SwitchToTargetingMode();
        }
        // -------------------

        // --- Input Delegation Methods (called by PlayerController) ---
        public void HandleEscapeKeyPress() => _uiEventMediator.HandleEscapeKeyPress();
        public void HandleEndTurnKeyPress() => _uiEventMediator.HandleEndTurnKeyPress();

        public string GetTargetingText(ActionState state) => state.ToString();

        public Card GetHoveredHandCard() => _interactionMapper.GetHoveredHandCard();
        public Card GetHoveredPlayedCard() => _interactionMapper.GetHoveredPlayedCard(_inputManagerBacking);
        public Card GetHoveredMarketCard() => _interactionMapper.GetHoveredMarketCard();
    }
}


