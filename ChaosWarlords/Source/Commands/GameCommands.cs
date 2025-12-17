using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using System; // Added for the PlayCardCommand switch statement

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        private readonly Card _card;
        public BuyCardCommand(Card card) { _card = card; }

        // FIX 2: Update signature and property access
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

        // FIX 2: Update signature and property access
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
            // FIX: Don't just flip the boolean. 
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

        // FIX 2: Update signature
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
        // FIX 2: Update signature
        public void Execute(IGameplayState state)
        {
            // Call the consolidated EndTurn logic on the state interface
            state.EndTurn();
        }
    }

    public class StartAssassinateCommand : IGameCommand
    {
        // FIX 2: Update signature and property access
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
        // FIX 2: Update signature and property access
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

        // FIX 2: Update signature and property access
        public void Execute(IGameplayState state)
        {
            // Note: Spy selection logic is usually handled directly in the UpdateSpySelectionLogic()
            // in the state itself. This command looks like it tries to finalize an action.
            // We should use the ActionSystem via the interface property.

            if (state.ActionSystem.FinalizeSpyReturn(_spyColor))
            {
                // The ActionCompletedCommand must now also take IGameplayState
                new ActionCompletedCommand().Execute(state);
            }
        }
    }

    public class CancelActionCommand : IGameCommand
    {
        // FIX 2: Update signature and property access
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
        // FIX 2: Update signature
        public void Execute(IGameplayState state)
        {
            state.SwitchToNormalMode();
        }
    }
}