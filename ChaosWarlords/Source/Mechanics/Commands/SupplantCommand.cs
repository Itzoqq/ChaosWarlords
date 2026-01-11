using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class SupplantCommand : IGameCommand
    {
        public int TargetNodeId { get; }
        public string? CardId { get; }
        public string? DevourCardId { get; }

        public SupplantCommand(int targetNodeId, string? cardId = null, string? devourCardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
            DevourCardId = devourCardId;
        }

        public bool Validate(MatchContext context)
        {
             // Supplant = Assassinate + Deploy
             var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
             var player = context.TurnManager.ActivePlayer;
             
             if (node == null) return false;
             
             // Must be able to Assassinate (implied have presence somewhere?)
             // And then Deploy (implied have presence there after?)
             // Strict rule: "Recall an enemy troop, then place one of your troops at that site."
             
             return context.MapManager.CanAssassinate(node, player); // && context.MapManager.CanDeployAt(node, player.Color) logic?
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            var player = context.TurnManager.ActivePlayer;
            if (node != null)
            {
                context.MapManager.Supplant(node, player);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
