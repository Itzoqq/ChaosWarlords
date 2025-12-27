using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Commands
{
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
}
