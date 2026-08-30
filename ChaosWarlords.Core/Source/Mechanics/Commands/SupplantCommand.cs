using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class SupplantCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.Supplant;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.SupplantCommandDto
            {
                NodeId = TargetNodeId,
                CardId = CardId,
                DevourCardId = DevourCardId
            };
        }

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
            // Supplant = Assassinate + Deploy: "Recall an enemy troop, then place one of your
            // troops at that site." Mirrors ActionInputController.HandleSupplant's checks so a
            // directly-dispatched command can't grant a supplant with no troop to place.
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            var player = context.TurnManager.ActivePlayer;

            if (node == null) return false;
            if (player.TroopsInBarracks <= 0) return false;

            return context.MapManager.CanAssassinate(node, player);
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node != null)
            {
                // Delegates to ActionSystem.PerformSupplant rather than duplicating the
                // MapManager/CompleteAction calls here, because that's also where the
                // transactional "Devour a card -> Supplant" handling lives (DevourCardId) -
                // see planning.txt KNOWN BUGS for why this matters: duplicating the logic
                // here previously meant a deferred devour (e.g. the Wight card) never
                // actually happened.
                context.ActionSystem.PerformSupplant(node, CardId, DevourCardId);
            }
        }
    }
}
