using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class AssassinateCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.Assassinate;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.AssassinateCommandDto
            {
                NodeId = TargetNodeId,
                CardId = CardId,
                DevourCardId = DevourCardId
            };
        }
        public int TargetNodeId { get; }
        public string? CardId { get; }
        public string? DevourCardId { get; }

        public AssassinateCommand(int targetNodeId, string? cardId = null, string? devourCardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
            DevourCardId = devourCardId;
        }

        public bool Validate(MatchContext context)
        {
            // 1. Get Node
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node == null) return false;

            // 2. Get Player
            var player = context.TurnManager.ActivePlayer; // Assassinate is usually active player action

            // 3. When not fed by a card, this costs Power - enforce that here (not just in the
            // input layer) so a directly-dispatched command can't grant a free assassination.
            if (string.IsNullOrEmpty(CardId) && player.Power < GameConstants.AssassinatePowerCost)
            {
                return false;
            }

            // 4. Delegation
            return context.MapManager.CanAssassinate(node, player);
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node != null)
            {
                // Delegates to ActionSystem.PerformAssassinate (Power cost + MapManager +
                // CompleteAction) rather than duplicating those calls here, because that's
                // also where the transactional "Devour a card -> Assassinate" handling
                // lives (DevourCardId) - see planning.txt KNOWN BUGS for why this matters:
                // duplicating the logic here previously meant a deferred devour never
                // actually happened.
                context.ActionSystem.PerformAssassinate(node, CardId, DevourCardId);
            }
        }
    }
}
