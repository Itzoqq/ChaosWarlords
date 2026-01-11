using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.GameStates;
using System.Linq;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Mediates between UI events and game state changes.
    /// Extracted from GameplayState to separate UI event handling from core game logic.
    /// Manages popup dialogs and pause menu state.
    /// Industry precedent: MVC Controller, MVVM ViewModel mediator pattern
    /// </summary>
    public class UIEventMediator : IUIEventMediator
    {
        private readonly IGameplayState _gameState;
        private readonly IUIManager _uiManager;
        private readonly IActionSystem _actionSystem;
        private readonly IGameLogger _logger;
        private readonly Game1? _game; // For main menu navigation

        // State
        private bool _isConfirmationPopupOpen;
        private bool _isPauseMenuOpen;

        public bool IsConfirmationPopupOpen 
        {
            get 
            {
                return _isConfirmationPopupOpen; 
            }
        }
        public bool IsPauseMenuOpen => _isPauseMenuOpen;

        public UIEventMediator(
            IGameplayState gameState,
            IUIManager uiManager,
            IActionSystem actionSystem,
            IGameLogger logger,
            Game1? game)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _game = game; // Can be null for testing
        }

        // Optional Effect Event
        public event Action<Entities.Cards.Card, Entities.Cards.CardEffect, Action, Action>? OnOptionalEffectRequested;

        public void RequestOptionalEffect(Entities.Cards.Card card, Entities.Cards.CardEffect effect, Action onAccept, Action onDecline)
        {
            OnOptionalEffectRequested?.Invoke(card, effect, onAccept, onDecline);
        }

        /// <summary>
        /// Subscribe to all UI events. Call this during initialization.
        /// </summary>
        public void Initialize()
        {
            // Unsubscribe first to prevent double-subscription
            Cleanup();

            // Game UI events
            _uiManager.OnMarketToggleRequest += HandleMarketToggle;
            _uiManager.OnAssassinateRequest += HandleAssassinateRequest;
            _uiManager.OnReturnSpyRequest += HandleReturnSpyRequest;
            _uiManager.OnEndTurnRequest += HandleEndTurnRequest;

            // Popup events
            _uiManager.OnPopupConfirm += HandlePopupConfirm;
            _uiManager.OnPopupCancel += HandlePopupCancel;

            // Pause menu events
            _uiManager.OnResumeRequest += HandleResumeRequest;
            _uiManager.OnMainMenuRequest += HandleMainMenuRequest;
            _uiManager.OnExitRequest += HandleExitRequest;

            // Action system events
            _actionSystem.OnActionCompleted += HandleActionCompleted;
            _actionSystem.OnActionFailed += HandleActionFailed;
        }

        /// <summary>
        /// Unsubscribe from all events. Call this during cleanup.
        /// </summary>
        public void Cleanup()
        {
            _uiManager.OnMarketToggleRequest -= HandleMarketToggle;
            _uiManager.OnAssassinateRequest -= HandleAssassinateRequest;
            _uiManager.OnReturnSpyRequest -= HandleReturnSpyRequest;
            _uiManager.OnEndTurnRequest -= HandleEndTurnRequest;
            _uiManager.OnPopupConfirm -= HandlePopupConfirm;
            _uiManager.OnPopupCancel -= HandlePopupCancel;
            _uiManager.OnResumeRequest -= HandleResumeRequest;
            _uiManager.OnMainMenuRequest -= HandleMainMenuRequest;
            _uiManager.OnExitRequest -= HandleExitRequest;

            _actionSystem.OnActionCompleted -= HandleActionCompleted;
            _actionSystem.OnActionFailed -= HandleActionFailed;
        }

        /// <summary>
        /// Update UI state synchronization. Call this each frame.
        /// </summary>
        public void Update()
        {
            _uiManager.IsPaused = _isPauseMenuOpen;
            _uiManager.IsPopupVisible = _isConfirmationPopupOpen;
        }

        // --- Public Methods for External Control ---

        public void HandleEscapeKeyPress()
        {
            if (_isPauseMenuOpen)
            {
                _isPauseMenuOpen = false;
                return;
            }

            // Priority 1: Cancel Targeting
            if (_actionSystem.IsTargeting())
            {
                _actionSystem.CancelTargeting();
                _gameState.SwitchToNormalMode();
                return; // Do NOT open pause menu
            }

            // Priority 2: Close Market
            if (_gameState.IsMarketOpen)
            {
                _gameState.MarketStateManager.Close();
                return; // Do NOT open pause menu
            }

            // Priority 3: Close Confirmation Popup
            if (_isConfirmationPopupOpen)
            {
                _isConfirmationPopupOpen = false;
                return; // Do NOT open pause menu
            }

            // If nothing else to cancel, Open Pause Menu
            _isPauseMenuOpen = true;
        }

        public void HandleEndTurnKeyPress()
        {
            // Check for unplayed cards first (same logic as HandleEndTurnRequest)
            bool hasUnplayedCards = _gameState.MatchContext.ActivePlayer.Hand.Count > 0;
            
            
            if (hasUnplayedCards)
            {
                _logger.Log("Gameplay: Opening Confirmation Popup", LogChannel.Info);
                _isConfirmationPopupOpen = true;
            }
            else
            {
                // No unplayed cards, check for promotions
                HandleEndTurnWithPromotionCheck();
            }
        }

        // --- Private Event Handlers ---

        private void HandleMarketToggle(object? sender, EventArgs e)
        {
            // Toggle between closed and browse mode
            if (_gameState.MarketStateManager.IsOpen)
                _gameState.MarketStateManager.Close();
            else
                _gameState.MarketStateManager.OpenForBrowsing();
        }

        private void HandleAssassinateRequest(object? sender, EventArgs e)
        {
            _actionSystem.TryStartAssassinate();
            if (_actionSystem.IsTargeting())
            {
                _gameState.SwitchToTargetingMode();
            }
        }

        private void HandleReturnSpyRequest(object? sender, EventArgs e)
        {
            _actionSystem.TryStartReturnSpy();
            if (_actionSystem.IsTargeting())
            {
                _gameState.SwitchToTargetingMode();
            }
        }

        private void HandleEndTurnRequest(object? sender, EventArgs e)
        {
            _logger.Log("Gameplay: EndTurn Request Received", LogChannel.Info);

            if (!_gameState.CanEndTurn(out string reason))
            {
                _logger.Log($"Cannot End Turn: {reason}", LogChannel.Warning);
                return;
            }

            bool hasUnplayedCards = _gameState.MatchContext.ActivePlayer.Hand.Count > 0;
            
            if (hasUnplayedCards)
            {
                _logger.Log("Gameplay: Opening Confirmation Popup", LogChannel.Info);
                _isConfirmationPopupOpen = true;
            }
            else
            {
                _logger.Log("Gameplay: Ending Turn Immediately", LogChannel.Info);
                HandleEndTurnWithPromotionCheck();
            }
        }

        private void HandlePopupConfirm(object? sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                _logger.Log("Gameplay: Popup Confirmed - Ending Turn", LogChannel.Info);
                _isConfirmationPopupOpen = false;
                HandleEndTurnWithPromotionCheck();
            }
        }

        private void HandlePopupCancel(object? sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                _logger.Log("Gameplay: Popup Cancelled", LogChannel.Info);
                _isConfirmationPopupOpen = false;
            }
        }

        private void HandleResumeRequest(object? sender, EventArgs e)
        {
            if (_isPauseMenuOpen) _isPauseMenuOpen = false;
        }

        private void HandleMainMenuRequest(object? sender, EventArgs e)
        {
            if (_isPauseMenuOpen && _game is not null)
            {
                // Properly create MainMenuState with view and button manager
                // This matches the initialization pattern in Game1.LoadContent()
                // This matches the initialization pattern in Game1.LoadContent()
                var buttonManager = new ChaosWarlords.Source.Rendering.UI.ButtonManager();
                var mainMenuView = new ChaosWarlords.Source.Rendering.Views.MainMenuView(
                    _game.GraphicsDevice,
                    _game.Content,
                    buttonManager,
                    _logger);

                var mainMenuState = new MainMenuState(
                    _game,
                    _game.InputProvider,
                    _game.StateManager,
                    _game.CardDatabase,
                    _game.ReplayManager,
                    _logger,
                    mainMenuView,
                    buttonManager);

                _game.StateManager.ChangeState(mainMenuState);
            }
        }

        private void HandleExitRequest(object? sender, EventArgs e)
        {
            if (_isPauseMenuOpen && _game is not null)
            {
                _game.Exit();
            }
        }

        private void HandleActionFailed(object? sender, string msg)
        {
            _logger.Log(msg, LogChannel.Error);
        }

        private void HandleActionCompleted(object? sender, EventArgs e)
        {
            if (_actionSystem.PendingCard is not null)
            {
                _gameState.MatchManager.PlayCard(_actionSystem.PendingCard);
            }
            // FIX: Do NOT call CancelTargeting here. 
            // 1. It wipes PendingDevourCard, breaking chained transactions.
            // 2. ActionSystem.CompleteAction() already calls ClearState(), which is safer.
            // _actionSystem.CancelTargeting();
            
            // Only switch to Normal Mode if Market is NOT open.
            // If Market is open (e.g. after Devour), we want to stay in MarketInputMode.
            if (!_gameState.IsMarketOpen)
            {
                _gameState.SwitchToNormalMode();
            }
        }

        private void HandleEndTurnWithPromotionCheck()
        {
            int pending = _gameState.MatchContext.TurnManager.CurrentTurnContext.PendingPromotionsCount;
            _logger.Log($"DEBUG: HandleEndTurnWithPromotionCheck. Pending: {pending}", LogChannel.Info);

            if (pending > 0)
            {
                var activePlayer = _gameState.MatchContext.TurnManager.ActivePlayer;
                _logger.Log($"DEBUG: ActivePlayer: {activePlayer.DisplayName}. PlayedCards: {activePlayer.PlayedCards.Count}", LogChannel.Info);
                
                bool hasValidTargets = activePlayer.PlayedCards.Any(c =>
                    _gameState.MatchContext.TurnManager.CurrentTurnContext.HasValidCreditFor(c));
                
                _logger.Log($"DEBUG: HasValidTargets: {hasValidTargets}", LogChannel.Info);

                if (hasValidTargets)
                {
                    _logger.Log($"You must promote {pending} card(s) before ending your turn.", LogChannel.Warning);
                    _gameState.SwitchToPromoteMode(pending);
                }
                else
                {
                    _logger.Log("No valid cards to promote. Promotion effects skipped.", LogChannel.Info);
                    // Create and execute EndTurn command through centralized system
                    var cmd = new ChaosWarlords.Source.Commands.EndTurnCommand();
                    _gameState.RecordAndExecuteCommand(cmd);
                }
            }
            else
            {
                // Create and execute EndTurn command through centralized system
                var cmd = new ChaosWarlords.Source.Commands.EndTurnCommand();
                _gameState.RecordAndExecuteCommand(cmd);
            }
        }
    }
}



