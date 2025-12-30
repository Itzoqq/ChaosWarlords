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
        private float _replayTimer;
        private const float _replayDelay = 0.2f; // 200ms between commands for visibility
        private bool _replayComplete; // Track if replay has finished

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
        private int _localSequenceCounter;
        
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
                _localSequenceCounter = 0;
            }

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
            _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, _replayManager, () => SwitchToTargetingMode(), _logger);

            _uiEventMediator = new UIEventMediator(this, _uiManagerBacking, _matchContext.ActionSystem, _logger, _game as Game1);
            _uiEventMediator.Initialize();

            _playerController = new PlayerController(this, _inputManagerBacking, _inputCoordinator, _interactionMapper);
        }

        private void HandleSetupDeploymentComplete()
        {
            if (_matchContext.CurrentPhase == MatchPhase.Setup)
            {
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
            // 1. Update input state (captures current frame's mouse/keyboard state)
            _inputManagerBacking.Update();

            // 2. Sync UI state (updates pause menu and popup flags)
            _uiEventMediator.Update();

            // 3. CRITICAL: Process UI clicks FIRST (highest priority)
            // This includes pause menu buttons, popups, and game UI buttons
            // UIManager will handle clicks and fire events (like OnMainMenuRequest)
            // SKIP during replay mode - only allow ESC to pause
            if (!_replayManager.IsReplaying)
            {
                _uiManagerBacking.Update(_inputManagerBacking);
            }

            // 4. If modal UI is open (pause or popup), BLOCK all other input
            // This prevents game input from processing while UI is active
            if (_uiEventMediator.IsPauseMenuOpen || _uiEventMediator.IsConfirmationPopupOpen)
            {
                // Still update view for visual feedback (hovers, animations)
                _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);
                return; // Exit early - no game input processing
            }

            // 5. DEBUG: F5 to Save Replay (Only after setup phase complete)
            if (_inputManagerBacking.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.F5))
            {
                if (_matchContext.CurrentPhase == MatchPhase.Setup)
                {
                    _logger.Log("Cannot save replay during setup phase! Complete initial deployment first.", LogChannel.Warning);
                }
                else if (!_replayManager.IsReplaying)
                {
                    string json = _replayManager.GetRecordingJson();
                    System.IO.File.WriteAllText("last_replay.json", json);
                    _logger.Log("Replay saved to last_replay.json", LogChannel.Info);
                }
            }
            
            // F6 to Load and Play Replay (Only at fresh game start, before any troops placed)
            if (_inputManagerBacking.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.F6))
            {
                // Check if any troops have been placed on the map
                bool anyTroopsPlaced = _matchContext.MapManager.Nodes.Any(n => n.Occupant != PlayerColor.None && n.Occupant != PlayerColor.Neutral);
                
                if (anyTroopsPlaced)
                {
                    // If troops are placed, we can't start/restart replay
                    if (_replayManager.IsReplaying || _replayComplete)
                    {
                        _logger.Log("Cannot restart replay mid-game! Exit to main menu and start a new game to replay again.", LogChannel.Warning);
                    }
                    else
                    {
                        _logger.Log("Cannot start replay after troops have been placed! Start a new game first.", LogChannel.Warning);
                    }
                }
                else if (System.IO.File.Exists("last_replay.json"))
                {
                    // Only allow starting replay if no troops placed (fresh game)
                    // Stop current replay if running (shouldn't happen, but safety check)
                    if (_replayManager.IsReplaying)
                    {
                        _replayManager.StopReplay();
                    }
                    
                    // Reset replay state
                    _replayComplete = false;
                    _replayTimer = 0f;
                    
                    // Start replay
                    string json = System.IO.File.ReadAllText("last_replay.json");
                    _replayManager.StartReplay(json);

                    // CRITICAL: Re-initialize the match with the seed from the replay!
                    InitializeMatch();
                    InitializeSystems();
                    
                    _logger.Log($"Replay started (Seed: {_replayManager.Seed}). Watch your previous game unfold!", LogChannel.Info);
                }
                else
                {
                    _logger.Log("No replay file found. Play a game and press F5 to save a replay first.", LogChannel.Warning);
                }
            }

            // 6. Process Input (OR Replay)
            if (_replayManager.IsReplaying)
            {
                // REPLAY MODE: Execute commands with a small delay for visibility
                _replayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                if (_replayTimer >= _replayDelay)
                {
                    _replayTimer = 0f;
                    
                    var cmd = _replayManager.GetNextCommand(this);
                    if (cmd != null)
                    {
                        // Execute the command directly
                        cmd.Execute(this);
                        _logger.Log($"Replay Executed: {cmd.GetType().Name} (ActivePlayer: {_matchContext.ActivePlayer.Color})", LogChannel.Info);
                        
                        // Force view update to show the command's effects immediately
                        _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);
                    }
                    else if (!_replayComplete)
                    {
                        // Replay has finished
                        _replayComplete = true;
                        _logger.Log("=== REPLAY COMPLETE === Press F6 to restart", LogChannel.Info);
                    }
                }
                
                // During replay, BLOCK all game input except ESC to exit
                // No player controller updates, no button clicks, no card interactions
            }
            else
            {
                // NORMAL MODE: Process game input (map clicks, card clicks, etc.)
                // Only reached if no modal UI is blocking
                _playerController.Update();
            }

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

            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, _uiManagerBacking, IsMarketOpen, targetingText, _uiEventMediator.IsConfirmationPopupOpen, _uiEventMediator.IsPauseMenuOpen, _replayManager.IsReplaying);

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
            // Record the command for replay (unless we're currently replaying)
            if (!_replayManager.IsReplaying)
            {
                _replayManager.RecordCommand(command, _matchContext.ActivePlayer, ++_localSequenceCounter);
            }

            // Execute the command
            command.Execute(this);
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


