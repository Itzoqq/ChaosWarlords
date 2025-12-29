using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Source.Commands
{
    public class DeployTroopCommand : IGameCommand
    {
        public MapNode Node { get; }
        public DeployTroopCommand(MapNode node) { Node = node; }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("Deploy", $"Deployed troop at Node {Node.Id}");
            // Accessing via interface properties
            state.MapManager.TryDeploy(state.TurnManager.ActivePlayer, Node);
        }
    }
}



