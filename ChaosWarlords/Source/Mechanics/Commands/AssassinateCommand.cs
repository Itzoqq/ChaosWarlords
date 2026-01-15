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
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node == null) return false;

            // 2. Get Player
            var player = context.TurnManager.ActivePlayer; // Assassinate is usually active player action

            // 3. Delegation
            return context.MapManager.CanAssassinate(node, player);
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            var player = context.TurnManager.ActivePlayer;
            if (node != null)
            {
                if (string.IsNullOrEmpty(CardId))
                {
                    context.PlayerStateManager.TrySpendPower(player, GameConstants.AssassinatePowerCost);
                }

                context.MapManager.Assassinate(node, player);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
