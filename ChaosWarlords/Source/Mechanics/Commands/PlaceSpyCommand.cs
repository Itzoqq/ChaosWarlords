using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class PlaceSpyCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.PlaceSpy;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.PlaceSpyCommandDto
            {
                SiteId = TargetSiteId,
                CardId = CardId
            };
        }
        public int TargetSiteId { get; }
        public string? CardId { get; }

        public PlaceSpyCommand(int targetSiteId, string? cardId = null)
        {
            TargetSiteId = targetSiteId;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return false;

            // Check if player has spies to place
            return context.TurnManager.ActivePlayer.SpiesInBarracks > 0;
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site != null)
            {
                context.MapManager.PlaceSpy(site, context.TurnManager.ActivePlayer);
                context.RecordAction("PlaceSpy", $"Placed spy at {site.Name}");
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
