using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Managers;


namespace ChaosWarlords.Source.Systems
{
    public class GameplayInputCoordinator : IGameplayInputCoordinator
    {
        private IInputMode _currentMode;
        private readonly GameplayState _state; // Reference back to main state for context
        private readonly InputManager _inputManager;
        private readonly MatchContext _context;

        public IInputMode CurrentMode => _currentMode;

        public GameplayInputCoordinator(GameplayState state, InputManager inputManager, MatchContext context)
        {
            _state = state;
            _inputManager = inputManager;
            _context = context;
            SwitchToNormalMode();
        }

        public void HandleInput()
        {
            IGameCommand command = _currentMode.HandleInput(
               _inputManager,
               _context.MarketManager,
               _context.MapManager,
               _context.ActivePlayer,
               _context.ActionSystem);

            command?.Execute(_state);
        }

        public void SwitchToNormalMode()
        {
            _currentMode = new NormalPlayInputMode(
                _state,
                _inputManager,
                _state.UIManager,
                _context.MapManager,
                _context.TurnManager as TurnManager,
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
                if (amount == 0 && _context.ActionSystem.PendingCard != null)
                    amount = 1; // Simplify for now

                _currentMode = new PromoteInputMode(_state, _inputManager, _context.ActionSystem, amount);
            }
            else if (_context.ActionSystem.CurrentState == Utilities.ActionState.TargetingDevourHand)
            {
                _currentMode = new DevourInputMode(_state, _inputManager, _context.ActionSystem);
            }
            else
            {
                _currentMode = new TargetingInputMode(
                    _state,
                    _inputManager,
                    _state.UIManager,
                    _context.MapManager,
                    _context.TurnManager as TurnManager,
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

