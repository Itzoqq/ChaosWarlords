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
        private readonly InputManager _inputManager;
        private readonly MatchContext _context;

        public IInputMode CurrentMode => _currentMode;

        public GameplayInputCoordinator(IGameplayState state, InputManager inputManager, MatchContext context)
        {
            _state = state;
            _inputManager = inputManager;
            _context = context;

            // Subscribe to state changes to auto-switch input modes
            _context.ActionSystem.OnStateChanged += HandleActionStateChanged;
            
            // Subscribe to market mode changes
            _state.MarketStateManager.ModeChanged += HandleMarketModeChanged;

            SwitchToNormalMode();
        }

        private void HandleActionStateChanged(object? sender, Utilities.ActionState newState)
        {
            _state.Logger.Log($"Coordinator: State Changed to {newState}. Switching Input Mode.", Utilities.LogChannel.Input);
            if (newState == Utilities.ActionState.Normal)
            {
                // If Market is Open (e.g. Browse), stay in/switch to MarketInputMode
                bool isMarketOpen = _state.MarketStateManager.IsOpen;
                _state.Logger.Log($"[Coordinator] HandleActionStateChanged: Normal. MarketOpen: {isMarketOpen}, CurrentMode: {_currentMode?.GetType().Name}", Utilities.LogChannel.Input);

                if (isMarketOpen)
                {
                    if (!(_currentMode is MarketInputMode))
                    {
                        _state.Logger.Log("[Coordinator] Enforcing MarketInputMode because Market is Open.", Utilities.LogChannel.Input);
                        _currentMode = new MarketInputMode(_state, _inputManager, _context);
                    }
                    else
                    {
                         _state.Logger.Log("[Coordinator] Already in MarketInputMode. Preserving.", Utilities.LogChannel.Input);
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
             if (_inputManager.IsLeftMouseJustClicked())
             {
                _state.Logger.Log($"[Coordinator] HandleInput Dispatching. Mode: {_currentMode?.GetType().Name}", Utilities.LogChannel.Input);
             }

             if (_currentMode == null) return;

             IGameCommand? command = _currentMode.HandleInput(
               _inputManager,
               _context.MarketManager,
               _context.MapManager,
               _context.ActivePlayer,
               _context.ActionSystem);

            if (command != null)
            {
                // Centralized command recording - ALL player commands flow through here
                _state.RecordAndExecuteCommand(command);
            }
        }

        public void SwitchToNormalMode()
        {
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
            if (_context.ActionSystem.CurrentState == Utilities.ActionState.SelectingCardToPromote)
            {
                int amount = _context.TurnManager.CurrentTurnContext.PendingPromotionsCount;
                // Fallback to card effect if context is 0 (direct play)
                if (amount == 0 && _context.ActionSystem.PendingCard is not null)
                    amount = 1; // Simplify for now

                _state.Logger.Log($"Coordinator: Switching to PromoteInputMode (Amount: {amount})", Utilities.LogChannel.Input);
                _currentMode = new PromoteInputMode(_state, _inputManager, _context.ActionSystem, amount);
            }
            else if (_context.ActionSystem.CurrentState == Utilities.ActionState.TargetingDevourHand)
            {
                _state.Logger.Log("Coordinator: Switching to DevourInputMode (Hand)", Utilities.LogChannel.Input);
                _currentMode = new DevourInputMode(_state, _inputManager, _context.ActionSystem);
            }
            // Note: TargetingDevourMarket is handled by MarketStateManager.ModeChanged event.
            else
            {
                _state.Logger.Log($"Coordinator: Switching to TargetingInputMode (State: {_context.ActionSystem.CurrentState})", Utilities.LogChannel.Input);
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
            _state.Logger.Log($"Coordinator: Market mode changed to {newMode}. Switching Input Mode.", Utilities.LogChannel.Input);

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
