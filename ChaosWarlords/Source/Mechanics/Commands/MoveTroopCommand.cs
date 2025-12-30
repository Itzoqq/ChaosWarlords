using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class MoveTroopCommand : IGameCommand
    {
        public int SourceNodeId { get; }
        public int DestinationNodeId { get; }
        public string? CardId { get; }

        public MoveTroopCommand(int sourceNodeId, int destinationNodeId, string? cardId = null)
        {
            SourceNodeId = sourceNodeId;
            DestinationNodeId = destinationNodeId;
            CardId = cardId;
        }

        public void Execute(IGameplayState state)
        {
            var source = state.MapManager.Nodes.FirstOrDefault(n => n.Id == SourceNodeId);
            var dest = state.MapManager.Nodes.FirstOrDefault(n => n.Id == DestinationNodeId);
            if (source != null && dest != null)
            {
                state.ActionSystem.PerformMoveTroop(source, dest, CardId);
                state.MatchContext?.RecordAction("MoveTroop", $"Moved troop from {source.Id} to {dest.Id}");
            }
        }
    }
}
