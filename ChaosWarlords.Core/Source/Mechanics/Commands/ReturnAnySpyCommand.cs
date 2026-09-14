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

            // This command has no basic-action shape at all - it only ever exists as
            // EffectType.ReturnUnitOrSpy's spy-side half (Intellect Devourer), so outside that
            // exact targeting state there is no real pending effect for it to resolve. Without
            // this, a directly-dispatched command could mutate the board (and, paired with the
            // Power-cost gap below, do so for free) with no genuine effect behind it at all.
            if (context.ActionSystem.CurrentState != ActionState.TargetingReturnUnitOrSpy)
            {
                return context.RejectValidation(nameof(ReturnAnySpyCommand), "no pending ReturnUnitOrSpy effect is open - this command is never a standalone basic action.");
            }

            var player = context.TurnManager.ActivePlayer;
            if (!context.MapManager.CanReturnAnySpy(site, player, SpyColor, RequiresEnemyOnly(context)))
            {
                return context.RejectValidation(nameof(ReturnAnySpyCommand), $"cannot return {SpyColor} spy at site '{site.Name}' (missing target, no Presence for an enemy spy, or own spy excluded by an enemy-only effect).");
            }
            return true;
        }

        // Re-derives the enemy-only restriction from the currently pending CardEffect (High
        // Priest of Myrkul) rather than trusting anything the caller claims - same pattern as
        // ReturnTroopCommand.RequiresEnemyOnly.
        private static bool RequiresEnemyOnly(MatchContext context)
        {
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.ReturnUnitOrSpy && pendingEffect.ReturnEnemyOnly;
        }

        // "Funded by a card" is re-derived from ActionSystem's own trusted CurrentSourceEffect
        // (same defense RequiresEnemyOnly above already uses) rather than trusted from a bare,
        // caller-suppliable non-empty CardId alone, so a forged command can't waive the Power
        // cost by merely naming a CardId with nothing actually pending.
        private static bool IsGenuinelyCardFunded(MatchContext context, string? cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.ReturnUnitOrSpy;
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return;

            var player = context.TurnManager.ActivePlayer;

            // The base "Return an enemy spy" action costs 3 Power when not funded by a card -
            // matching ResolveSpyCommand's identical rule. Returning your OWN spy is never a
            // paid basic action at all (only ever card-driven), matching ReturnOwnSpyCommand's
            // own no-cost design - so no cost applies here either way when SpyColor is the
            // active player's own color. Affordability is checked BEFORE mutating the board;
            // the actual spend still only happens AFTER the map mutation succeeds, so a failed
            // ReturnAnySpy never costs Power for nothing, and an unaffordable one never
            // mutates the board.
            bool isOwnSpy = SpyColor == player.Color;
            bool isPaidByCard = IsGenuinelyCardFunded(context, CardId);
            bool costApplies = !isOwnSpy && !isPaidByCard;

            if (costApplies && player.Power < GameConstants.ReturnSpyPowerCost)
            {
                context.ActionSystem.NotifyFailure($"Not enough Power to return spy! (Need {GameConstants.ReturnSpyPowerCost})");
                return;
            }

            if (context.MapManager.ReturnAnySpy(site, player, SpyColor))
            {
                if (costApplies)
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
