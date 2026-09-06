using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;


namespace ChaosWarlords.Source.Input
{
    public class GameplayInputCoordinator : IGameplayInputCoordinator
    {
        private IInputMode _currentMode = null!;
        private readonly IGameplayState _state; // Reference back to main state for context
        private readonly IInputManager _inputManager;
        private readonly MatchContext _context;

        public IInputMode CurrentMode => _currentMode;

        public GameplayInputCoordinator(IGameplayState state, IInputManager inputManager, MatchContext context)
        {
            _state = state;
            _inputManager = inputManager;
            _context = context;

            // Subscribe to state changes to auto-switch input modes
            _context.ActionSystem.OnStateChanged += HandleActionStateChanged;

            // Subscribe to market mode changes
            _state.MarketStateManager.ModeChanged += HandleMarketModeChanged;

            // NEW: Subscribe to Input Events
            _inputManager.OnInputEvent += HandleInputEvent;

            SwitchToNormalMode();
        }

        private void HandleInputEvent(object? sender, Core.Events.InputEventArgs e)
        {
            // BLOCKING CHECK: while any overlay/popup is open, this is the ONLY handler for
            // Escape/Enter/click - PlayerController used to ALSO independently react to the
            // same raw event here, re-checking this same blocking state itself; that looked
            // "mutually exclusive" on paper but wasn't in practice, since one handler's own
            // side effect (e.g. this method's fallback below opening the pause menu) could
            // flip the very flag the OTHER handler was about to check for the SAME event,
            // causing it to immediately undo what was just done (verified: Escape opening the
            // pause menu, then PlayerController's own stale-blocking-check-turned-true
            // re-invoking HandleEscapeKeyPress a second time and closing it right back). Fixed
            // by making this the single, un-bypassable handler for ALL of it - see
            // planning.txt.
            if (_state.IsPauseMenuOpen || _state.IsConfirmationPopupOpen || _state.IsOptionalEffectPopupOpen)
            {
                HandleBlockedInput(e);
                return;
            }

            if (_currentMode == null) return;

            // Delegate event to current mode - RightClick/Escape/Enter flow through here
            // exactly like LeftClick already does, so the active IInputMode is the single,
            // un-bypassable source of truth for what cancelling/declining means right now.
            // See planning.txt for why a second, competing global handler used to exist.
            IGameCommand? command = _currentMode.HandleInteraction(
                e,
                _context.MarketManager,
                _context.MapManager,
                _context.ActivePlayer,
                _context.ActionSystem);

            if (command != null)
            {
                _state.Logger.Log($"[Coordinator] Command Generated from {e.Type}: {command.GetType().Name}", LogChannel.Input);
                _state.RecordAndExecuteCommand(command);
                return;
            }

            // Nothing about this event was handled by the active mode - the only remaining
            // universal meanings are Escape (open the pause menu) and Enter (attempt to end
            // the turn). RightClick/LeftClick with nothing to do are legitimately no-ops.
            HandleUnhandledGlobalShortcut(e);
        }

        /// <summary>
        /// Escape closes/declines whichever overlay is open (pause menu first, then
        /// confirmation popup, then optional-effect popup - see IGameplayState.
        /// HandleEscapeKeyPress's own priority order), Enter confirms the simple yes/no
        /// confirmation popup specifically (matches the pre-existing behavior - the optional-
        /// effect popup has no implicit Enter-confirm, only its own explicit accept/decline),
        /// and a LeftClick on the optional-effect popup routes to its own click handler. This
        /// is the ONLY place any of this runs - see HandleInputEvent's own comment on why a
        /// second, independent handler for the same events was a real bug, not just a
        /// theoretical one.
        /// </summary>
        private void HandleBlockedInput(Core.Events.InputEventArgs e)
        {
            if (e.Type != Core.Events.InputEventType.KeyDown)
            {
                HandleBlockedLeftClick(e);
                return;
            }

            if (e.Key == Keys.Escape)
            {
                HandleBlockedEscape();
            }
            else if (e.Key == Keys.Enter)
            {
                HandleBlockedEnter();
            }
        }

        private void HandleBlockedEscape()
        {
            _state.HandleEscapeKeyPress();
        }

        private void HandleBlockedEnter()
        {
            if (_state.IsConfirmationPopupOpen)
            {
                _state.UIManager.TriggerPopupConfirm();
            }
        }

        private void HandleBlockedLeftClick(Core.Events.InputEventArgs e)
        {
            if (e.Type != Core.Events.InputEventType.LeftClick) return;
            if (!_state.IsOptionalEffectPopupOpen || _state.View == null) return;

            var mousePos = e.Position.ToPoint();
            _state.View.HandleOptionalEffectClick(mousePos.X, mousePos.Y);
        }

        /// <summary>
        /// Fallback for Escape/Enter ONLY when the active IInputMode declined to produce a
        /// command for it - e.g. nothing is being targeted (NormalPlayInputMode never
        /// intercepts these) or a mandatory sequence refused to cancel (PromoteInputMode/
        /// DiscardInputMode both correctly return null in that case, which safely falls
        /// through to "open the pause menu" here - pausing never bypasses a mandatory effect,
        /// it just pauses). Reuses IGameplayState.HandleEscapeKeyPress - the same method
        /// HandleBlockedInput calls for the popup/pause-open case - since both call sites are
        /// mutually exclusive in this single method's own control flow (one blocking check,
        /// one branch or the other, never both for the same event) and want identical "close
        /// confirm popup -> decline optional popup -> else open pause menu" behavior.
        /// </summary>
        private void HandleUnhandledGlobalShortcut(Core.Events.InputEventArgs e)
        {
            if (e.Type == Core.Events.InputEventType.KeyDown && e.Key == Keys.Escape)
            {
                _state.HandleEscapeKeyPress();
                return;
            }

            if (e.Type == Core.Events.InputEventType.KeyDown && e.Key == Keys.Enter)
            {
                if (_state.CanEndTurn(out string reason))
                {
                    _state.HandleEndTurnKeyPress();
                }
                else
                {
                    _state.Logger.Log(reason, LogChannel.Warning);
                }
            }
        }

        private void HandleActionStateChanged(object? sender, ActionState newState)
        {
            _state.Logger.Log($"Coordinator: State Changed to {newState}. Switching Input Mode.", LogChannel.Input);
            if (newState == ActionState.Normal)
            {
                // If Market is Open (e.g. Browse), stay in/switch to MarketInputMode
                bool isMarketOpen = _state.MarketStateManager.IsOpen;
                _state.Logger.Log($"[Coordinator] HandleActionStateChanged: Normal. MarketOpen: {isMarketOpen}, CurrentMode: {_currentMode?.GetType().Name}", LogChannel.Input);

                if (isMarketOpen)
                {
                    if (!(_currentMode is MarketInputMode))
                    {
                        _state.Logger.Log("[Coordinator] Enforcing MarketInputMode because Market is Open.", LogChannel.Input);
                        _currentMode = new MarketInputMode(_state, _inputManager, _context);
                    }
                    else
                    {
                        _state.Logger.Log("[Coordinator] Already in MarketInputMode. Preserving.", LogChannel.Input);
                    }
                }
                else
                {
                    SwitchToNormalMode();
                }
            }
            else
            {
                SwitchToTargetingMode();
            }
        }

        public void HandleInput()
        {
            // "HandleInput" is now effectively "Update" for continuous input (Hover, Drag)
            // Discrete input is handled by HandleInputEvent
            
            if (_currentMode != null)
            {
                _currentMode.HandleUpdate(_inputManager, _context.MapManager, _context.ActivePlayer);
            }
        }

        public void SwitchToNormalMode()
        {
            if (_state.MarketStateManager.IsOpen)
            {
                _state.Logger.Log("[Coordinator] SwitchToNormalMode called, but Market is Open. Enforcing MarketInputMode.", LogChannel.Input);
                _currentMode = new MarketInputMode(_state, _inputManager, _context);
                return;
            }

            _currentMode = new NormalPlayInputMode(
                _state,
                _inputManager,
                _state.UIManager,
                _context.MapManager,
                _context.TurnManager,
                _context.ActionSystem
            );
        }

        public void SwitchToTargetingMode()
        {
            var state = _context.ActionSystem.CurrentState;

            _currentMode = state switch
            {
                ActionState.SelectingCardToPromote => CreatePromoteMode(),
                ActionState.TargetingDevourHand or ActionState.TargetingDevourInnerCircle => CreateDevourMode(state),
                ActionState.TargetingDiscard => CreateDiscardMode(),
                ActionState.TargetingPromoteFromPile => CreatePromoteFromPileMode(),
                // Both Devour-from-Market and Play-from-Market (Ulitharid) are handled by
                // ActionSystem calling MarketStateManager.OpenForDevour, which triggers
                // HandleMarketModeChanged below - just switch to TargetingInputMode
                // temporarily, same as the default case, with a more specific log message.
                ActionState.TargetingDevourMarket or ActionState.TargetingPlayFromMarket => CreateMarketPendingMode(state),
                _ => CreateDefaultTargetingMode(state),
            };
        }

        private PromoteInputMode CreatePromoteMode()
        {
            int amount = _context.TurnManager.CurrentTurnContext.PendingPromotionsCount;
            // Fallback to card effect if context is 0 (direct play)
            if (amount == 0 && _context.ActionSystem.PendingCard is not null)
                amount = 1; // Simplify for now

            _state.Logger.Log($"Coordinator: Switching to PromoteInputMode (Amount: {amount})", LogChannel.Input);
            return new PromoteInputMode(_state, _inputManager, _context.ActionSystem, amount);
        }

        private DevourInputMode CreateDevourMode(ActionState state)
        {
            _state.Logger.Log($"Coordinator: Switching to DevourInputMode (State: {state})", LogChannel.Input);
            return new DevourInputMode(_state, _inputManager, _context.ActionSystem);
        }

        private DiscardInputMode CreateDiscardMode()
        {
            _state.Logger.Log("Coordinator: Switching to DiscardInputMode.", LogChannel.Input);
            return new DiscardInputMode(_state, _inputManager, _context.ActionSystem);
        }

        private PromoteFromPileInputMode CreatePromoteFromPileMode()
        {
            _state.Logger.Log("Coordinator: Switching to PromoteFromPileInputMode.", LogChannel.Input);
            return new PromoteFromPileInputMode(_state, _inputManager, _context.ActionSystem);
        }

        private TargetingInputMode CreateMarketPendingMode(ActionState state)
        {
            _state.Logger.Log($"Coordinator: {state} detected. Market will open via MarketStateManager.", LogChannel.Input);
            return CreateTargetingInputMode();
        }

        private TargetingInputMode CreateDefaultTargetingMode(ActionState state)
        {
            _state.Logger.Log($"Coordinator: Switching to TargetingInputMode (State: {state})", LogChannel.Input);
            return CreateTargetingInputMode();
        }

        private TargetingInputMode CreateTargetingInputMode() => new(
            _state,
            _inputManager,
            _state.UIManager,
            _context.MapManager,
            _context.TurnManager,
            _context.ActionSystem
        );

        private void HandleMarketModeChanged(object? sender, MarketMode newMode)
        {
            _state.Logger.Log($"Coordinator: Market mode changed to {newMode}. Switching Input Mode.", LogChannel.Input);

            switch (newMode)
            {
                case MarketMode.Closed:
                    // Market closed - switch to normal mode
                    SwitchToNormalMode();
                    break;

                case MarketMode.Browse:
                    // Normal browsing/buying mode - create MarketInputMode without callback
                    _currentMode = new MarketInputMode(_state, _inputManager, _context);
                    break;

                case MarketMode.DevourTarget:
                    // Devour targeting mode - MarketInputMode will retrieve callback from MarketStateManager
                    _currentMode = new MarketInputMode(_state, _inputManager, _context);
                    break;
            }
        }
    }
}
