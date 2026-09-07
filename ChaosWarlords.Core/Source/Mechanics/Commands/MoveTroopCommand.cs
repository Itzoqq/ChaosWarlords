using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class MoveTroopCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.MoveTroop;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.MoveTroopCommandDto
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
            var src = context.MapManager.GetNodeById(SourceNodeId);
            var dest = context.MapManager.GetNodeById(DestinationNodeId);

            if (src == null || dest == null)
            {
                return context.RejectValidation(nameof(MoveTroopCommand), $"source node {SourceNodeId} or destination node {DestinationNodeId} not found.");
            }

            var player = context.TurnManager.ActivePlayer;

            // 2. Delegate to MapManager logic - checked separately (not a single && expression)
            // so a rejection log can say WHICH half failed.
            if (!context.MapManager.CanMoveSource(src, player))
            {
                return context.RejectValidation(nameof(MoveTroopCommand), $"MapManager rejected source node {SourceNodeId} (no Presence, or nothing to move).");
            }
            if (!context.MapManager.CanMoveDestination(dest))
            {
                return context.RejectValidation(nameof(MoveTroopCommand), $"MapManager rejected destination node {DestinationNodeId} (not empty).");
            }
            return true;
        }

        public void Execute(MatchContext context)
        {
            var src = context.MapManager.GetNodeById(SourceNodeId);
            var dest = context.MapManager.GetNodeById(DestinationNodeId);
            var player = context.TurnManager.ActivePlayer;

            if (src != null && dest != null)
            {
                context.MapManager.MoveTroop(src, dest, player);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
