using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Returns one of the active player's OWN spies from a site (e.g. Cloaker's "return one
    /// of your spies" half). Sets ActionSystem.PendingSite on success so a chained effect
    /// (Assassinate, scoped to "at that spy's site") can read it back - see
    /// ActionInputController.HandleAssassinate's PendingSite guard.
    /// </summary>
    public class ReturnOwnSpyCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ReturnOwnSpy;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ReturnOwnSpyCommandDto
            {
                SiteId = TargetSiteId,
                CardId = CardId
            };
        }

        public int TargetSiteId { get; }
        public string? CardId { get; }

        public ReturnOwnSpyCommand(int targetSiteId, string? cardId = null)
        {
            TargetSiteId = targetSiteId;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return false;

            return context.MapManager.CanReturnOwnSpy(site, context.TurnManager.ActivePlayer);
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return;

            if (context.MapManager.ReturnOwnSpy(site, context.TurnManager.ActivePlayer))
            {
                context.RecordAction("ReturnOwnSpy", $"Returned own spy from {site.Name}.");
                context.ActionSystem.SetPendingSiteForChain(site);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
