using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class PlaceSpyCommand : IGameCommand
    {
        public int TargetSiteId { get; }
        public string? CardId { get; }

        public PlaceSpyCommand(int targetSiteId, string? cardId = null)
        {
            TargetSiteId = targetSiteId;
            CardId = cardId;
        }

        public void Execute(IGameplayState state)
        {
            var site = state.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site != null)
            {
                state.ActionSystem.PerformPlaceSpy(site, CardId);
                state.MatchContext?.RecordAction("PlaceSpy", $"Placed spy at {site.Name}");
            }
        }
    }
}
