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

            // Mirrors ActionInputController.HandleReturn's checks: can't return an unoccupied or
            // Neutral-occupied node, and the requester must have presence to contest it.
            if (node.Occupant == Utilities.PlayerColor.None || node.Occupant == Utilities.PlayerColor.Neutral)
            {
                return false;
            }

            var player = context.TurnManager.ActivePlayer;
            return context.MapManager.HasPresence(node, player.Color);
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
