using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;
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

            SwitchToNormalMode();
        }

        private void HandleActionStateChanged(object? sender, Utilities.ActionState newState)
        {
            _state.Logger.Log($"Coordinator: State Changed to {newState}. Switching Input Mode.", Utilities.LogChannel.Input);
            if (newState == Utilities.ActionState.Normal)
            {
                SwitchToNormalMode();
            }
            else
            {
                SwitchToTargetingMode();
            }
        }

        public void HandleInput()
        {
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
                _state.Logger.Log("Coordinator: Switching to DevourInputMode", Utilities.LogChannel.Input);
                _currentMode = new DevourInputMode(_state, _inputManager, _context.ActionSystem);
            }
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

        public void SetMarketMode(bool isOpen)
        {
            if (isOpen)
                _currentMode = new MarketInputMode(_state, _inputManager, _context);
            else
                SwitchToNormalMode();
        }
    }
}

