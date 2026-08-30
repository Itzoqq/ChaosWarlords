using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class ResolveSpyCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ResolveSpy;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ResolveSpyCommandDto
            {
                SiteId = SiteId,
                Color = SpyColor.ToString(),
                CardId = CardId
            };
        }
        public int SiteId { get; }
        public PlayerColor SpyColor { get; }
        public string? CardId { get; }

        public ResolveSpyCommand(int siteId, PlayerColor spyColor, string? cardId = null)
        {
            SiteId = siteId;
            SpyColor = spyColor;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            // Logic: Can we return this spy?
            // Site.HasSpy(SpyColor)
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == SiteId);
            return site != null && site.HasSpy(SpyColor);
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == SiteId);
            if (site != null)
            {
                if (context.MapManager.ReturnSpecificSpy(site, context.TurnManager.ActivePlayer, SpyColor))
                {
                    if (string.IsNullOrEmpty(CardId))
                    {
                        context.PlayerStateManager.TrySpendPower(context.TurnManager.ActivePlayer, GameConstants.ReturnSpyPowerCost);
                    }
                    context.ActionSystem.CompleteAction();
                }
                else
                {
                    context.ActionSystem.NotifyFailure("Failed to return spy.");
                }
            }
        }
    }
}
