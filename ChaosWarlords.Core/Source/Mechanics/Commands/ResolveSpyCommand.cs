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
            if (site == null) return context.RejectValidation(nameof(ResolveSpyCommand), $"site {SiteId} not found.");
            if (!site.HasSpy(SpyColor))
            {
                return context.RejectValidation(nameof(ResolveSpyCommand), $"{SpyColor} has no spy at site '{site.Name}'.");
            }

            // This command only ever reaches an ENEMY spy via the real click path
            // (SpySubsystem.HandleReturnSpyInitialClick pre-filters candidates through
            // MapManager.GetEnemySpiesAtSite before ever constructing this command) - re-derived
            // here too, rather than trusting the caller, since Validate() is the real defense
            // once a client can send commands directly. Without this, a forged command naming
            // the active player's own spy color would have succeeded (their own spy is always
            // "present" at HasSpy, with no further ownership check below this point).
            if (SpyColor == context.TurnManager.ActivePlayer.Color)
            {
                return context.RejectValidation(nameof(ResolveSpyCommand), "cannot target your own spy - use ReturnOwnSpyCommand instead.");
            }
            return true;
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
