using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Contexts;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class MoveTroopCommand : IGameCommand
    {
        public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.MoveTroop;

        public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new ChaosWarlords.Source.Core.Data.Dtos.MoveTroopCommandDto
            {
                SrcId = SourceNodeId,
                DestId = DestinationNodeId,
                CardId = CardId
            };
        }
        public int SourceNodeId { get; }
        public int DestinationNodeId { get; }
        public string? CardId { get; }

        public MoveTroopCommand(int sourceNodeId, int destinationNodeId, string? cardId = null)
        {
            SourceNodeId = sourceNodeId;
            DestinationNodeId = destinationNodeId;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
             // 1. Get Nodes
             var src = context.MapManager.Nodes.FirstOrDefault(n => n.Id == SourceNodeId);
             var dest = context.MapManager.Nodes.FirstOrDefault(n => n.Id == DestinationNodeId);
             
             if (src == null || dest == null) return false;
             
             var player = context.TurnManager.ActivePlayer;
             
             // 2. Delegate to MapManager logic
             return context.MapManager.CanMoveSource(src, player) && context.MapManager.CanMoveDestination(dest);
        }

        public void Execute(MatchContext context)
        {
            var src = context.MapManager.Nodes.FirstOrDefault(n => n.Id == SourceNodeId);
            var dest = context.MapManager.Nodes.FirstOrDefault(n => n.Id == DestinationNodeId);
            var player = context.TurnManager.ActivePlayer;

            if (src != null && dest != null)
            {
                context.MapManager.MoveTroop(src, dest, player);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
