using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Returns a spy of ANY color (the active player's own, or an enemy's) from a site -
    /// EffectType.ReturnUnitOrSpy's spy-side half (Intellect Devourer: "Return up to two troops
    /// or spies"). Distinct from ReturnOwnSpyCommand (own spy only) and ResolveSpyCommand (the
    /// enemy-only 3-Power basic action) - this is the first command that treats "which spy" as a
    /// single, symmetric choice, mirroring ReturnTroopCommand/MapManager.CanReturnTroop's already-
    /// symmetric own/enemy handling for troops.
    /// </summary>
    public class ReturnAnySpyCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ReturnAnySpy;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ReturnAnySpyCommandDto
            {
                SiteId = TargetSiteId,
                Color = SpyColor.ToString(),
                CardId = CardId
            };
        }

        public int TargetSiteId { get; }
        public PlayerColor SpyColor { get; }
        public string? CardId { get; }

        public ReturnAnySpyCommand(int targetSiteId, PlayerColor spyColor, string? cardId = null)
        {
            TargetSiteId = targetSiteId;
            SpyColor = spyColor;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return context.RejectValidation(nameof(ReturnAnySpyCommand), $"site {TargetSiteId} not found.");

            var player = context.TurnManager.ActivePlayer;
            if (!context.MapManager.CanReturnAnySpy(site, player, SpyColor))
            {
                return context.RejectValidation(nameof(ReturnAnySpyCommand), $"cannot return {SpyColor} spy at site '{site.Name}' (missing target, or no Presence for an enemy spy).");
            }
            return true;
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return;

            var player = context.TurnManager.ActivePlayer;
            if (context.MapManager.ReturnAnySpy(site, player, SpyColor))
            {
                // The base "Return an enemy spy" action costs 3 Power when not funded by a
                // card - matching ResolveSpyCommand's identical rule. Returning your OWN spy is
                // never a paid basic action at all (only ever card-driven), matching
                // ReturnOwnSpyCommand's own no-cost design - so no cost applies here either way
                // when SpyColor is the active player's own color.
                bool isOwnSpy = SpyColor == player.Color;
                bool isPaidByCard = !string.IsNullOrEmpty(CardId);
                if (!isOwnSpy && !isPaidByCard)
                {
                    context.PlayerStateManager.TrySpendPower(player, GameConstants.ReturnSpyPowerCost);
                }

                context.RecordAction("ReturnAnySpy", $"Returned {SpyColor} spy from {site.Name}.");
                context.ActionSystem.CompleteAction();
            }
            else
            {
                context.ActionSystem.NotifyFailure("Failed to return spy.");
            }
        }
    }
}
