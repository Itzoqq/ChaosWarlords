using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Source.Commands
{
    public class DeployTroopCommand : IGameCommand
    {
        private readonly MapNode _node;
        public DeployTroopCommand(MapNode node) { _node = node; }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("Deploy", $"Deployed troop at Node {_node.Id}");
            // Accessing via interface properties
            state.MapManager.TryDeploy(state.TurnManager.ActivePlayer, _node);
        }
    }
}



