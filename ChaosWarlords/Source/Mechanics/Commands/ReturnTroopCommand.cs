using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class ReturnTroopCommand : IGameCommand
    {
        public int TargetNodeId { get; }
        public string? CardId { get; }

        public ReturnTroopCommand(int targetNodeId, string? cardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
        }

        public void Execute(IGameplayState state)
        {
            var node = state.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node != null)
            {
                state.ActionSystem.PerformReturnTroop(node, CardId);
                state.MatchContext?.RecordAction("ReturnTroop", $"Returned troop at {node.Id}");
            }
        }
    }
}
