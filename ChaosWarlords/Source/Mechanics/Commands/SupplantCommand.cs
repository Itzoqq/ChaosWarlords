using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class SupplantCommand : IGameCommand
    {
        public int TargetNodeId { get; }
        public string? CardId { get; }

        public SupplantCommand(int targetNodeId, string? cardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
        }

        public void Execute(IGameplayState state)
        {
            var node = state.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node != null)
            {
                state.ActionSystem.PerformSupplant(node, CardId);
                state.MatchContext?.RecordAction("Supplant", $"Supplanted troop at {node.Id}");
            }
        }
    }
}
