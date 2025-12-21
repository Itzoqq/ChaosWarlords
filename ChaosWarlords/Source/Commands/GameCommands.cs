using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        private readonly Card _card;
        public BuyCardCommand(Card card) { _card = card; }

        public void Execute(IGameplayState state)
        {
            // Accessing via interface properties
            state.MarketManager.TryBuyCard(state.TurnManager.ActivePlayer, _card);
        }
    }

    public class DeployTroopCommand : IGameCommand
    {
        private readonly MapNode _node;
        public DeployTroopCommand(MapNode node) { _node = node; }

        public void Execute(IGameplayState state)
        {
            // Accessing via interface properties
            state.MapManager.TryDeploy(state.TurnManager.ActivePlayer, _node);
        }
    }

    public class ToggleMarketCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            // Don't just flip the boolean. 
            // Call the methods that handle the State Transition logic.

            if (state.IsMarketOpen)
            {
                state.CloseMarket(); // This sets IsMarketOpen=false AND switches to NormalPlayInputMode
            }
            else
            {
                state.ToggleMarket(); // This sets IsMarketOpen=true AND switches to MarketInputMode
            }
        }
    }

    public class PlayCardCommand : IGameCommand
    {
        private readonly Card _card;
        public PlayCardCommand(Card card) { _card = card; }

        public void Execute(IGameplayState state)
        {
            // We can now call the PlayCard logic directly on the state interface
            // The logic inside PlayCard handles all the checks, targeting switches,
            // and final resolution.
            state.PlayCard(_card);
        }
    }

    public class EndTurnCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            // Validation Check
            if (state.CanEndTurn(out string reason))
            {
                state.EndTurn();
            }
            else
            {
                GameLogger.Log(reason, LogChannel.Warning);
            }
        }
    }

    public class StartAssassinateCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.ActionSystem.TryStartAssassinate();
            if (state.ActionSystem.CurrentState == ActionState.TargetingAssassinate)
            {
                state.SwitchToTargetingMode();
            }
        }
    }

    public class StartReturnSpyCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.ActionSystem.TryStartReturnSpy();
            if (state.ActionSystem.CurrentState == ActionState.TargetingReturnSpy)
            {
                state.SwitchToTargetingMode();
            }
        }
    }

    // Needed for the Spy Selection Popup
    public class ResolveSpyCommand : IGameCommand
    {
        private readonly PlayerColor _spyColor;
        public ResolveSpyCommand(PlayerColor spyColor) { _spyColor = spyColor; }

        public void Execute(IGameplayState state)
        {
            // We just call the method. 
            // If it succeeds, ActionSystem fires OnActionCompleted.
            // If it fails, ActionSystem fires OnActionFailed.
            // The GameplayState listens to these events and handles the rest.
            state.ActionSystem.FinalizeSpyReturn(_spyColor);
        }
    }

    public class CancelActionCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.ActionSystem.CancelTargeting();
            state.SwitchToNormalMode();
        }
    }

    public class SwitchToNormalModeCommand : IGameCommand
    {
        /// <summary>
        /// Executes a switch back to normal input mode. Used to break out of incorrect input modes.
        /// </summary>
        public void Execute(IGameplayState state)
        {
            state.SwitchToNormalMode();
        }
    }
}