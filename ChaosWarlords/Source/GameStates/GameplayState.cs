#nullable enable
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Contexts;

using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Core.Interfaces.Composition;
using ChaosWarlords.Source.Core.Composition;
using System;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IGameplayState, IDrawableState
    {
        private readonly Game? _game;

        private readonly ICardDatabase _cardDatabase;
        private readonly IGameLogger _logger;
        private readonly int _viewportWidth;
        private readonly int _viewportHeight;

        internal IGameplayView? _view;
        internal IMatchManager _matchManager = null!;
        internal MatchContext _matchContext = null!;
        internal InputManager _inputManagerBacking = null!;
        internal IUIManager _uiManagerBacking = null!;
        internal bool _isMarketOpenBacking;

        // New Coordinators
        internal GameplayInputCoordinator _inputCoordinator = null!;
        internal InteractionMapper? _interactionMapper;
        internal CardPlaySystem _cardPlaySystem = null!;
        internal PlayerController _playerController = null!;
        internal UIEventMediator _uiEventMediator = null!;

        public IInputManager InputManager => _inputManagerBacking;
        public IGameLogger Logger => _logger;
        public IUIManager UIManager => _uiManagerBacking;
        public IMatchManager MatchManager => _matchManager;

        public IMapManager MapManager => _matchContext?.MapManager!;
        public IMarketManager MarketManager => _matchContext?.MarketManager!;
        public IActionSystem ActionSystem => _matchContext?.ActionSystem!;
        public ITurnManager TurnManager => _matchContext?.TurnManager!;
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

        public GameplayState(IGameDependencies dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependencies);
            
            _game = dependencies.Game;
            _inputManagerBacking = (InputManager)dependencies.InputManager; // Cast for now as internal usage relies on specific class features if any, or just assign interface
            // Actually _inputManagerBacking is defined as InputManager internal field. 
            // We should check if we can change that field to IInputManager or if we need the cast.
            // Looking at the file, _inputManagerBacking is InputManager. 
            // The interface IGameDependencies returns IInputManager.
            // If InputManager implementation is required by other internal parts, we cast. 
            // Better practice: Change internal fields to interfaces.
            // For now, let's cast to keep changes minimal, but ideally we refactor the fields too.
            // Wait, dependencies.InputManager comes from Game1 which creates 'new InputManager', so it's safe.
            
            // To be cleaner, let's try to stick to interfaces. 
            // However, InputManagerBacking is passed to many internal coordinate systems.
            // Let's assume strict cast for now or update the field type.
            // Updating the field type to IInputManager is safer.
            
            // Re-reading file... 
            // internal InputManager _inputManagerBacking = null!;
            // public IInputManager InputManager => _inputManagerBacking;
            
            // I will update the constructor to Use dependencies. 
            // I'll keep the logic simple.
            
            if (dependencies.InputManager is InputManager concretInput)
                _inputManagerBacking = concretInput;
            else
                throw new ArgumentException("GameplayState currently requires concrete InputManager", nameof(dependencies));
            
            _cardDatabase = dependencies.CardDatabase;
            _logger = dependencies.Logger ?? throw new InvalidOperationException("Dependency Logger must not be null");
            _uiManagerBacking = dependencies.UIManager;
            _view = dependencies.View;
            _viewportWidth = dependencies.ViewportWidth;
            _viewportHeight = dependencies.ViewportHeight;
        }

        public void LoadContent()
        {
            // Game might be null in headless mode, but if we need to load content we might need a ContentProvider.
            // For now, checks are strict:
            if (_game is null && _view is not null)
            {
                // If view exists but game is null, we can't load content.
                _logger.Log("GameplayState: Skipping view content load because Game is null (Headless mode?)");
                return;
            }

            // InitializeInfrastructure(); // REMOVED
            InitializeView();
            InitializeMatch();
            InitializeSystems();
        }

        private void InitializeView()
        {
            if (_view is not null)
            {
                // Content loading is managed by State lifecycle
                if (_game is not null) _view.LoadContent(_game.Content);
                _interactionMapper = new InteractionMapper(_view);
            }
        }

        private void InitializeMatch()
        {
            var builder = new MatchFactory(_cardDatabase, _logger);
            var worldData = builder.Build();

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase,
                worldData.PlayerStateManager,
                _logger
            );

            _matchManager = new MatchManager(_matchContext, _logger);

            // Don't draw cards during Setup phase
            if (_matchContext.CurrentPhase != MatchPhase.Setup && _matchContext.TurnManager.Players is not null)
            {
                foreach (var player in _matchContext.TurnManager.Players)
                {
                    player.DrawCards(5, _matchContext.Random);
                }
            }

            _matchContext.MapManager.CenterMap(_viewportWidth, _viewportHeight);

            // Subscribe to Setup auto-advance
            _matchContext.MapManager.OnSetupDeploymentComplete += HandleSetupDeploymentComplete;
        }

        private void InitializeSystems()
        {
            _inputCoordinator = new GameplayInputCoordinator(this, _inputManagerBacking, _matchContext);
            _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, () => SwitchToTargetingMode(), _logger);

            _uiEventMediator = new UIEventMediator(this, _uiManagerBacking, _matchContext.ActionSystem, _logger, _game as Game1);
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
            // 1. Update input state (captures current frame's mouse/keyboard state)
            _inputManagerBacking.Update();

            // 2. Sync UI state (updates pause menu and popup flags)
            _uiEventMediator.Update();

            // 3. CRITICAL: Process UI clicks FIRST (highest priority)
            // This includes pause menu buttons, popups, and game UI buttons
            // UIManager will handle clicks and fire events (like OnMainMenuRequest)
            _uiManagerBacking.Update(_inputManagerBacking);

            // 4. If modal UI is open (pause or popup), BLOCK all other input
            // This prevents game input from processing while UI is active
            if (_uiEventMediator.IsPauseMenuOpen || _uiEventMediator.IsConfirmationPopupOpen)
            {
                // Still update view for visual feedback (hovers, animations)
                _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);
                return; // Exit early - no game input processing
            }

            // 5. Process game input (map clicks, card clicks, etc.)
            // Only reached if no modal UI is blocking
            _playerController.Update();

            // 6. Update view (card hovers, animations, etc.)
            _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // if (_game is null) return; // Allow drawing via View even if Game is null (for tests/tools)

            string targetingText = "";
            if (_matchContext.ActionSystem.IsTargeting())
            {
                targetingText = GetTargetingText(_matchContext.ActionSystem.CurrentState);
            }

            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, _uiManagerBacking, IsMarketOpen, targetingText, _uiEventMediator.IsConfirmationPopupOpen, _uiEventMediator.IsPauseMenuOpen);

            // Phase 0 UI Overlay
            if (_matchContext.CurrentPhase == MatchPhase.Setup)
            {
                _view?.DrawSetupPhaseOverlay(spriteBatch, _matchContext.TurnManager.ActivePlayer);
            }
        }

        public void PlayCard(Card card)
        {
            ArgumentNullException.ThrowIfNull(card);
            _cardPlaySystem.PlayCard(card);
        }

        public bool HasViableTargets(Card card) => _cardPlaySystem.HasViableTargets(card);



        public void MoveCardToPlayed(Card card)
        {
            ArgumentNullException.ThrowIfNull(card);
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

        public void SwitchToPromoteMode(int amount)
        {
            ArgumentNullException.ThrowIfNull(_matchContext, nameof(_matchContext));
            ArgumentNullException.ThrowIfNull(_matchContext.ActionSystem, nameof(_matchContext.ActionSystem));

            // We must set the ActionSystem state explicitly so the InputCoordinator knows 
            // to instantiate the PromoteInputMode instead of the generic TargetingInputMode.
            // We pass 'null' for the card because Promotion is a turn-phase action, not a card-specific action.
            _matchContext.ActionSystem.StartTargeting(ActionState.SelectingCardToPromote, null);

            _inputCoordinator.SwitchToTargetingMode();
        }

        // --- Input Delegation Methods (called by PlayerController) ---
        public void HandleEscapeKeyPress() => _uiEventMediator.HandleEscapeKeyPress();
        public void HandleEndTurnKeyPress() => _uiEventMediator.HandleEndTurnKeyPress();

        public string GetTargetingText(ActionState state) => state.ToString();

        public Card? GetHoveredHandCard() => _interactionMapper?.GetHoveredHandCard();
        public Card? GetHoveredPlayedCard() => _interactionMapper?.GetHoveredPlayedCard(_inputManagerBacking);
        public Card? GetHoveredMarketCard() => _interactionMapper?.GetHoveredMarketCard();
    }
}


