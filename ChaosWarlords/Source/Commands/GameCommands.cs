using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public interface IGameCommand
    {
        void Execute(GameplayState state);
    }

    public class BuyCardCommand : IGameCommand
    {
        private readonly Card _card;
        public BuyCardCommand(Card card) { _card = card; }

        public void Execute(GameplayState state)
        {
            // Accessing via interface property
            state._marketManager.TryBuyCard(state._turnManager.ActivePlayer, _card);
        }
    }

    public class DeployTroopCommand : IGameCommand
    {
        private readonly MapNode _node;
        public DeployTroopCommand(MapNode node) { _node = node; }

        public void Execute(GameplayState state)
        {
            // FIX: Access ActivePlayer via TurnManager
            state._mapManager.TryDeploy(state._turnManager.ActivePlayer, _node);
        }
    }

    public class ToggleMarketCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            // Toggles the boolean flag in GameplayState
            state._isMarketOpen = !state._isMarketOpen;
        }
    }

    public class PlayCardCommand : IGameCommand
    {
        private readonly Card _card;
        public PlayCardCommand(Card card) { _card = card; }

        public void Execute(GameplayState state)
        {
            state.PlayCard(_card);
        }
    }

    public class MapNodeClickedCommand : IGameCommand
    {
        private readonly MapNode _node;
        public MapNodeClickedCommand(MapNode node) { _node = node; }

        public void Execute(GameplayState state)
        {
            if (state._actionSystem.IsTargeting())
            {
                // Site retrieval still needs the concrete MapManager implementation, 
                // but since the interface exposes GetSiteForNode, this is fine.
                Site site = state._mapManager.GetSiteForNode(_node);
                bool success = state._actionSystem.HandleTargetClick(_node, site);

                // This is being called from TargetingInputMode, which now calls the ActionCompletedCommand itself.
                // This command should not be executed in TargetingInputMode, but since the UIManager can return it 
                // when in Normal mode, we leave the command completion logic here, but the game is designed 
                // to execute this command when the TARGETING mode returns it. 
                // Since this command is now only returned by UIManager in the Normal Mode path,
                // we leave the old deployment logic, but the root cause is the missing mode switch, which is now fixed.

                if (success) new ActionCompletedCommand().Execute(state); // <-- Should not be reached in fixed flow
            }
            else
            {
                // This path should only be executed in NormalPlayInputMode's HandleMapInteraction 
                // which is why the deployment logic is here.
                state._mapManager.TryDeploy(state._turnManager.ActivePlayer, _node);
            }
        }
    }

    public class SiteClickedCommand : IGameCommand
    {
        private readonly Site _site;
        public SiteClickedCommand(Site site) { _site = site; }

        public void Execute(GameplayState state)
        {
            // Accessing via interface property
            if (state._actionSystem.IsTargeting())
            {
                // Pass null for node, but valid site
                bool success = state._actionSystem.HandleTargetClick(null, _site);
                if (success) new ActionCompletedCommand().Execute(state);
            }
        }
    }

    public class StartAssassinateCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            state._actionSystem.TryStartAssassinate();
            if (state._actionSystem.CurrentState == ActionState.TargetingAssassinate) // Check if the call succeeded (e.g., affordable)
            {
                state.SwitchToTargetingMode(); // <-- ADDED
            }
        }
    }

    public class StartReturnSpyCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            state._actionSystem.TryStartReturnSpy();
            if (state._actionSystem.CurrentState == ActionState.TargetingReturnSpy)
            {
                state.SwitchToTargetingMode(); // <-- ADDED
            }
        }
    }

    // Needed for the Spy Selection Popup
    public class ResolveSpyCommand : IGameCommand
    {
        private readonly PlayerColor _spyColor;
        public ResolveSpyCommand(PlayerColor spyColor) { _spyColor = spyColor; }

        public void Execute(GameplayState state)
        {
            if (state._actionSystem.FinalizeSpyReturn(_spyColor))
            {
                new ActionCompletedCommand().Execute(state);
            }
        }
    }

    public class CancelActionCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            state._actionSystem.CancelTargeting();
            state.SwitchToNormalMode(); // <-- ADDED
        }
    }

    public class SwitchToNormalModeCommand : IGameCommand
    {
        /// <summary>
        /// Executes a switch back to normal input mode. Used to break out of incorrect input modes.
        /// </summary>
        public void Execute(GameplayState state)
        {
            state.SwitchToNormalMode();
        }
    }
}