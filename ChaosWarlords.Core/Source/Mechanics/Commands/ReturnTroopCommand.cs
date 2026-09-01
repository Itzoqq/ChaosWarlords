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
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node == null) return false;

            // Delegates to MapManager.CanReturnTroop - the single authoritative check
            // (occupied, not Neutral, and Presence required only for an enemy troop, not the
            // requester's own). This used to reimplement those conditions independently,
            // which is exactly how the Presence-for-own-troops bug (see CanReturnTroop's
            // comment) could have been fixed in one of the two places and not the other.
            return context.MapManager.CanReturnTroop(node, context.TurnManager.ActivePlayer);
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node != null)
            {
                context.MapManager.ReturnTroop(node, context.TurnManager.ActivePlayer);
                context.RecordAction("ReturnTroop", $"Returned troop at {node.Id}");
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
