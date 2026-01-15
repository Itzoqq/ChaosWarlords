using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class ReturnTroopCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ReturnTroop;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ReturnTroopCommandDto
            {
                NodeId = TargetNodeId,
                CardId = CardId
            };
        }
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
            return node.Occupant != Utilities.PlayerColor.None;
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
