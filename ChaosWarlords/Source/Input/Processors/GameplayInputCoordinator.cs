using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Logic;


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
            // BLOCKING CHECK: If any overlay/popup is open, do not process gameplay input here.
            // UI interactions are handled by PlayerController or UIManager.
            if (_state.IsPauseMenuOpen || _state.IsConfirmationPopupOpen || _state.IsOptionalEffectPopupOpen)
            {
                return;
            }

            if (_currentMode == null) return;

            // Delegate event to current mode
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
            // Specialized logic for which targeting mode to enter
            if (_context.ActionSystem.CurrentState == ActionState.SelectingCardToPromote)
            {
                int amount = _context.TurnManager.CurrentTurnContext.PendingPromotionsCount;
                // Fallback to card effect if context is 0 (direct play)
                if (amount == 0 && _context.ActionSystem.PendingCard is not null)
                    amount = 1; // Simplify for now

                _state.Logger.Log($"Coordinator: Switching to PromoteInputMode (Amount: {amount})", LogChannel.Input);
                _currentMode = new PromoteInputMode(_state, _inputManager, _context.ActionSystem, amount);
            }
            else if (_context.ActionSystem.CurrentState == ActionState.TargetingDevourHand ||
                     _context.ActionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                _state.Logger.Log($"Coordinator: Switching to DevourInputMode (State: {_context.ActionSystem.CurrentState})", LogChannel.Input);
                _currentMode = new DevourInputMode(_state, _inputManager, _context.ActionSystem);
            }
            else if (_context.ActionSystem.CurrentState == ActionState.TargetingDevourMarket)
            {
                // Market devour is handled by DevourSubsystem calling MarketStateManager.OpenForDevour
                // which triggers HandleMarketModeChanged event. Just switch to TargetingInputMode temporarily.
                _state.Logger.Log($"Coordinator: TargetingDevourMarket detected. Market will open via MarketStateManager.", LogChannel.Input);
                _currentMode = new TargetingInputMode(
                    _state,
                    _inputManager,
                    _state.UIManager,
                    _context.MapManager,
                    _context.TurnManager,
                    _context.ActionSystem
                );
            }
            else
            {
                _state.Logger.Log($"Coordinator: Switching to TargetingInputMode (State: {_context.ActionSystem.CurrentState})", LogChannel.Input);
                _currentMode = new TargetingInputMode(
                    _state,
                    _inputManager,
                    _state.UIManager,
                    _context.MapManager,
                    _context.TurnManager,
                    _context.ActionSystem
                );
            }
        }

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
