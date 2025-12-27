using System;
using ChaosWarlords.Source.Interfaces;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using System.Linq;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Mediates between UI events and game state changes.
    /// Extracted from GameplayState to separate UI event handling from core game logic.
    /// Manages popup dialogs and pause menu state.
    /// Industry precedent: MVC Controller, MVVM ViewModel mediator pattern
    /// </summary>
    public class UIEventMediator
    {
        private readonly IGameplayState _gameState;
        private readonly IUIManager _uiManager;
        private readonly IActionSystem _actionSystem;
        private readonly Game1 _game; // For main menu navigation

        // State
        private bool _isConfirmationPopupOpen = false;
        private bool _isPauseMenuOpen = false;

        public bool IsConfirmationPopupOpen => _isConfirmationPopupOpen;
        public bool IsPauseMenuOpen => _isPauseMenuOpen;

        public UIEventMediator(
            IGameplayState gameState,
            IUIManager uiManager,
            IActionSystem actionSystem,
            Game1 game)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _game = game; // Can be null for testing
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
            }
            else
            {
                _isPauseMenuOpen = true;
                if (_gameState.IsMarketOpen) _gameState.CloseMarket();
                _actionSystem.CancelTargeting();
                _gameState.SwitchToNormalMode();
                if (_isConfirmationPopupOpen) _isConfirmationPopupOpen = false;
            }
        }

        public void HandleEndTurnKeyPress()
        {
            // Check for unplayed cards first (same logic as HandleEndTurnRequest)
            bool hasUnplayedCards = _gameState.MatchContext.ActivePlayer.Hand.Count > 0;
            if (hasUnplayedCards)
            {
                GameLogger.Log("Gameplay: Opening Confirmation Popup", LogChannel.Info);
                _isConfirmationPopupOpen = true;
            }
            else
            {
                // No unplayed cards, check for promotions
                HandleEndTurnWithPromotionCheck();
            }
        }

        // --- Private Event Handlers ---

        private void HandleMarketToggle(object sender, EventArgs e)
        {
            _gameState.ToggleMarket();
        }

        private void HandleAssassinateRequest(object sender, EventArgs e)
        {
            _actionSystem.TryStartAssassinate();
            if (_actionSystem.IsTargeting())
            {
                _gameState.SwitchToTargetingMode();
            }
        }

        private void HandleReturnSpyRequest(object sender, EventArgs e)
        {
            _actionSystem.TryStartReturnSpy();
            if (_actionSystem.IsTargeting())
            {
                _gameState.SwitchToTargetingMode();
            }
        }

        private void HandleEndTurnRequest(object sender, EventArgs e)
        {
            GameLogger.Log("Gameplay: EndTurn Request Received", LogChannel.Info);
            bool hasUnplayedCards = _gameState.MatchContext.ActivePlayer.Hand.Count > 0;
            if (hasUnplayedCards)
            {
                GameLogger.Log("Gameplay: Opening Confirmation Popup", LogChannel.Info);
                _isConfirmationPopupOpen = true;
            }
            else
            {
                GameLogger.Log("Gameplay: Ending Turn Immediately", LogChannel.Info);
                HandleEndTurnWithPromotionCheck();
            }
        }

        private void HandlePopupConfirm(object sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                GameLogger.Log("Gameplay: Popup Confirmed - Ending Turn", LogChannel.Info);
                _isConfirmationPopupOpen = false;
                HandleEndTurnWithPromotionCheck();
            }
        }

        private void HandlePopupCancel(object sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                GameLogger.Log("Gameplay: Popup Cancelled", LogChannel.Info);
                _isConfirmationPopupOpen = false;
            }
        }

        private void HandleResumeRequest(object sender, EventArgs e)
        {
            if (_isPauseMenuOpen) _isPauseMenuOpen = false;
        }

        private void HandleMainMenuRequest(object sender, EventArgs e)
        {
            if (_isPauseMenuOpen && _game != null)
            {
                _game.StateManager.ChangeState(new MainMenuState(_game));
            }
        }

        private void HandleExitRequest(object sender, EventArgs e)
        {
            if (_isPauseMenuOpen && _game != null)
            {
                _game.Exit();
            }
        }

        private void HandleActionFailed(object sender, string msg)
        {
            GameLogger.Log(msg, LogChannel.Error);
        }

        private void HandleActionCompleted(object sender, EventArgs e)
        {
            if (_actionSystem.PendingCard != null)
            {
                _gameState.MatchManager.PlayCard(_actionSystem.PendingCard);
            }
            _actionSystem.CancelTargeting();
            _gameState.SwitchToNormalMode();
        }

        private void HandleEndTurnWithPromotionCheck()
        {
            int pending = _gameState.TurnManager.CurrentTurnContext.PendingPromotionsCount;
            if (pending > 0)
            {
                var activePlayer = _gameState.TurnManager.ActivePlayer;
                bool hasValidTargets = activePlayer.PlayedCards.Any(c =>
                    _gameState.TurnManager.CurrentTurnContext.HasValidCreditFor(c));

                if (hasValidTargets)
                {
                    GameLogger.Log($"You must promote {pending} card(s) before ending your turn.", LogChannel.Warning);
                    _gameState.SwitchToPromoteMode(pending);
                }
                else
                {
                    GameLogger.Log("No valid cards to promote. Promotion effects skipped.", LogChannel.Info);
                    _gameState.EndTurn();
                }
            }
            else
            {
                _gameState.EndTurn();
            }
        }
    }
}
