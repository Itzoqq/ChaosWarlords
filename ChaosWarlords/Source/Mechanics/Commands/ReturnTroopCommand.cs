using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
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

        public bool Validate(MatchContext context)
        {
             var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
             if (node == null) return false;
             
             // Check if node has occupant
             return node.Occupant != ChaosWarlords.Source.Utilities.PlayerColor.None;
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node != null)
            {
                context.MapManager.ReturnTroop(node, context.TurnManager.ActivePlayer);
                context.RecordAction("ReturnTroop", $"Returned troop at {node.Id}");
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
