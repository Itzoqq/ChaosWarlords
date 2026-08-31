using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.GameStates;

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
        private bool _isOptionalEffectPopupOpen;

        // Callbacks for Optional Effect
        private Action? _onOptionalEffectAccept;
        private Action? _onOptionalEffectDecline;

        public bool IsConfirmationPopupOpen => _isConfirmationPopupOpen;
        public bool IsOptionalEffectPopupOpen => _isOptionalEffectPopupOpen;
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

        // Optional Effect Event (View listens to this for content)
        public event Action<Entities.Cards.Card, Entities.Cards.CardEffect, Action, Action>? OnOptionalEffectRequested;

        /// <summary>
        /// Subscribed to ActionSystem.OnInteractionRequested (see Initialize/Cleanup). This is
        /// the only place logic-layer optional-effect requests reach the UI - ActionSystem
        /// itself never calls into IUIEventMediator directly. Forwards into the existing
        /// RequestOptionalEffect popup flow, resolving the request via its OnResponse callback
        /// instead of the separate onAccept/onDecline delegates that flow took directly.
        /// </summary>
        private void HandleInteractionRequest(Core.Contexts.InteractionRequest request)
        {
            RequestOptionalEffect(
                request.SourceCard,
                request.SourceEffect,
                onAccept: () => request.OnResponse(true),
                onDecline: () => request.OnResponse(false));
        }

        public void RequestOptionalEffect(Entities.Cards.Card card, Entities.Cards.CardEffect effect, Action onAccept, Action onDecline)
        {
            // Update State
            _isOptionalEffectPopupOpen = true;
            _onOptionalEffectAccept = onAccept;
            _onOptionalEffectDecline = onDecline;

            // Wrap callbacks for the View so that if the View triggers them directly,
            // we properly clear our internal state.
            Action wrappedAccept = () => 
            {
                ClearOptionalPopupState();
                onAccept?.Invoke();
            };

            Action wrappedDecline = () =>
            {
                ClearOptionalPopupState();
                onDecline?.Invoke();
            };

            // Notify View to gather content
            OnOptionalEffectRequested?.Invoke(card, effect, wrappedAccept, wrappedDecline);
        }

        private void ClearOptionalPopupState()
        {
            _isOptionalEffectPopupOpen = false;
            _onOptionalEffectAccept = null;
            _onOptionalEffectDecline = null;
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
            _actionSystem.OnInteractionRequested += HandleInteractionRequest;
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
            _actionSystem.OnInteractionRequested -= HandleInteractionRequest;
        }

        /// <summary>
        /// Update UI state synchronization. Call this each frame.
        /// </summary>
        public void Update()
        {
            _uiManager.IsPaused = _isPauseMenuOpen;
            // Visible if EITHER popup is open - gates the Main Game UI buttons (Market/
            // Assassinate/ReturnSpy/EndTurn), which must stay disabled for both popup types.
            _uiManager.IsPopupVisible = _isConfirmationPopupOpen || _isOptionalEffectPopupOpen;
            // Confirmation-popup-ONLY - gates UIManager's generic PopupConfirmButtonRect/
            // PopupCancelButtonRect, which must NOT also activate while the optional-effect
            // popup is open (it has its own dedicated Yes/No buttons - see IUIManager's doc
            // comment on this property).
            _uiManager.IsConfirmationPopupVisible = _isConfirmationPopupOpen;
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
            
            // Priority 4: Decline Optional Effect
            if (_isOptionalEffectPopupOpen)
            {
                HandlePopupCancel(this, EventArgs.Empty);
                return;
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
            else if (_isOptionalEffectPopupOpen)
            {
                // Not reachable via mouse click today: UIManager's generic popup-confirm
                // button is gated on IsConfirmationPopupVisible, not the combined
                // IsPopupVisible, specifically so it stays inactive while only the
                // optional-effect popup is open (see IUIManager.IsConfirmationPopupVisible's
                // doc comment - this used to double-fire alongside OptionalEffectPopup's own
                // Yes/No buttons on the same click). Kept as a defensive fallback in case
                // OnPopupConfirm ever gets triggered another way (e.g. a future keybind).
                _logger.Log("Gameplay: Optional Effect Accepted via Popup", LogChannel.Info);
                var callback = _onOptionalEffectAccept;
                ClearOptionalPopupState();
                callback?.Invoke();
            }
        }

        private void HandlePopupCancel(object? sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                _logger.Log("Gameplay: Popup Cancelled", LogChannel.Info);
                _isConfirmationPopupOpen = false;
            }
            else if (_isOptionalEffectPopupOpen)
            {
                _logger.Log("Gameplay: Optional Effect Declined via Popup", LogChannel.Info);
                var callback = _onOptionalEffectDecline;
                ClearOptionalPopupState();
                callback?.Invoke();
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
                var buttonManager = new Rendering.UI.ButtonManager();
                var mainMenuView = new Rendering.Views.MainMenuView(
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
            // Historically this re-invoked MatchManager.PlayCard(_actionSystem.PendingCard)
            // here - a leftover from before PlayCardCommand.Execute existed as the one real
            // call site. By the time OnActionCompleted fires for any effect that sets
            // PendingCard (see ActionSystem.HandleInputRequiredEffect), the card has ALWAYS
            // already been moved out of Hand: PendingCard is only ever set while processing
            // effects pushed by CardEffectProcessor.ResolveEffects, and ResolveEffects only
            // ever runs from inside MatchManager.PlayCard, which moves the card to Played as
            // its very first step. So this call was always a no-op (MatchManager.PlayCard's
            // own Hand.Contains guard silently swallowed it) - except for the mandatory
            // devour-from-hand pre-commit flow (DevourInputMode.HandlePreCommitSelection),
            // which used to call ActionSystem.CompleteAction() before the card was played,
            // making this reentrant call briefly load-bearing (and fragile) for that one
            // flow. That premature CompleteAction() call is gone now (see DevourInputMode's
            // comment), so this call is unconditionally dead weight - removed. It used to
            // also log a spurious "Attempted to play card X which is NOT in active player's
            // hand" Error on every targeting/optional card's completion (see planning.txt).

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
                    var cmd = new Commands.EndTurnCommand();
                    _gameState.RecordAndExecuteCommand(cmd);
                }
            }
            else
            {
                // Create and execute EndTurn command through centralized system
                var cmd = new Commands.EndTurnCommand();
                _gameState.RecordAndExecuteCommand(cmd);
            }
        }
    }
}



