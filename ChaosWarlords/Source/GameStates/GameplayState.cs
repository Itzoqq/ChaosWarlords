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
        internal readonly IReplayManager _replayManager;
        private readonly IGameLogger _logger;
        private readonly int _viewportWidth;
        private readonly int _viewportHeight;

        // Replay timing
        // Replay Controller
        internal ReplayController _replayController = null!;

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
        internal ICommandDispatcher _commandDispatcher = null!;
        
        // 1. Viewport Settings
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
            _replayManager = dependencies.ReplayManager ?? throw new ArgumentException("ReplayManager must not be null", nameof(dependencies));
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
            // Use seed from replay if we are replaying, otherwise generate new one
            int? seedToUse = _replayManager.IsReplaying ? _replayManager.Seed : (int?)null;
            
            var builder = new MatchFactory(_cardDatabase, _logger);
            var worldData = builder.Build(_replayManager, seedToUse);

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase,
                worldData.PlayerStateManager,
                _logger,
                worldData.Seed
            );

            // Initialize recording if we're NOT replaying
            if (!_replayManager.IsReplaying)
            {
                _replayManager.InitializeRecording(_matchContext.Seed);
            }

            var victoryManager = new VictoryManager(_logger);
            _matchManager = new MatchManager(_matchContext, _logger, victoryManager);
            
            // Connect ActionSystem to MatchManager
            _matchContext.ActionSystem.SetMatchManager(_matchManager);

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
            _commandDispatcher = new CommandDispatcher(_replayManager, _logger);
            _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, _replayManager, () => SwitchToTargetingMode(), _logger);

            _uiEventMediator = new UIEventMediator(this, _uiManagerBacking, _matchContext.ActionSystem, _logger, _game as Game1);
            _uiEventMediator.Initialize();

            _playerController = new PlayerController(this, _inputManagerBacking, _inputCoordinator, _interactionMapper);

            // Initialize Replay Controller with callback to reload match
            _replayController = new ReplayController(this, _replayManager, _inputManagerBacking, _logger, () =>
            {
                InitializeMatch();
                // We MUST re-initialize systems that depend on MatchContext (like InputCoordinator)
                InitializeSystems(); 
            });
        }

        private void HandleSetupDeploymentComplete()
        {
            if (_matchContext.CurrentPhase == MatchPhase.Setup)
            {
                // Fix for Replay Desync:
                // During Replay, the recorded command stream already contains the EndTurnCommand (Seq 2).
                // If we auto-generate it here, we advance the turn twice (once here, once in replay stream).
                if (_replayManager.IsReplaying) return;

                // Create and execute EndTurn command through centralized system
                var cmd = new ChaosWarlords.Source.Commands.EndTurnCommand();
                RecordAndExecuteCommand(cmd);
            }
        }

        public void UnloadContent()
        {
            _uiEventMediator?.Cleanup();
        }

        public void Update(GameTime gameTime)
        {
            // 1. Update systems
            _inputManagerBacking.Update();
            _uiEventMediator.Update();

            // 2. Process UI Interaction (Highest Priority)
            if (!_replayManager.IsReplaying)
            {
                _uiManagerBacking.Update(_inputManagerBacking);
            }

            // 3. Check for Blocking UI
            if (_uiEventMediator.IsPauseMenuOpen || _uiEventMediator.IsConfirmationPopupOpen)
            {
                _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);
                return;
            }

            // 4. Update Replay Controller (Handles F5/F6 and Playback Loop)
            _replayController.Update(gameTime);

            // 5. Normal Game Input (Only if not replaying)
            if (!_replayManager.IsReplaying)
            {
                _playerController.Update();
            }

            // 6. Update View
            _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);

            // 7. Check Victory Condition
            if (MatchManager.VictoryResult != null && !_replayManager.IsReplaying)
            {
                // Transition to Victory Screen
                // We use ChangeState to replace GameplayState with VictoryState
                if (_game is Game1 game1)
                {
                    game1.StateManager.ChangeState(new ChaosWarlords.Source.States.VictoryState(game1, MatchManager.VictoryResult));
                }
            }
        }







        public void Draw(SpriteBatch spriteBatch)
        {
            // if (_game is null) return; // Allow drawing via View even if Game is null (for tests/tools)

            string targetingText = "";
            if (_matchContext.ActionSystem.IsTargeting())
            {
                targetingText = GetTargetingText(_matchContext.ActionSystem.CurrentState);
            }

            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, _uiManagerBacking, IsMarketOpen, targetingText, _uiEventMediator.IsConfirmationPopupOpen, _uiEventMediator.IsPauseMenuOpen, _replayManager.IsReplaying, MatchManager);

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

        // ===== Command Execution =====

        /// <summary>
        /// Centralized command execution point - ALL player commands flow through here.
        /// Automatically records commands for replay before executing them.
        /// </summary>
        public void RecordAndExecuteCommand(ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand command)
        {
            _commandDispatcher.Dispatch(command, this);
        }

        public void EndTurn()
        {
            if (_matchContext.ActionSystem.IsTargeting()) _matchContext.ActionSystem.CancelTargeting();
            _matchManager.EndTurn();
            SwitchToNormalMode();
        }

        public void ToggleMarket() { IsMarketOpen = !IsMarketOpen; }
        public void CloseMarket() { IsMarketOpen = false; }

        public void SwitchToTargetingMode()
        {
            // During replay, we do not want to activate targeting mode (UI/Input handling).
            // The commands will execute directly.
            if (_replayManager.IsReplaying) return;

            _inputCoordinator.SwitchToTargetingMode();
        }

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


