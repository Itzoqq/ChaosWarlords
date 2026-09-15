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

            // Re-derives "genuinely open" from ActionSystem's own trusted state rather than
            // trusting the caller (docs/coding-guidelines.md Rule #25) - this command is only
            // ever produced by SpySubsystem.HandleReturnSpyInitialClick (single enemy spy,
            // CurrentState still TargetingReturnSpy) or ActionSystem.FinalizeSpyReturn's
            // non-ReturnUnitOrSpy branch (2+ enemy spies, CurrentState left at
            // SelectingSpyToReturn by TransitionToSpySelection) - see FinalizeSpyReturn's own
            // doc comment (not TransitionToSpySelection's, which is a plain one-liner) for why
            // SelectingSpyToReturn is shared with ReturnAnySpyCommand's own disambiguation and
            // must be told apart via CurrentSourceEffect, not CurrentState alone.
            if (!IsGenuineReturnSpyState(context))
            {
                return context.RejectValidation(nameof(ResolveSpyCommand), "no pending 'Return an enemy spy' effect is open - this command is never a standalone free action.");
            }
            return true;
        }

        // See Validate()'s doc comment above for why SelectingSpyToReturn alone isn't enough -
        // it's shared with EffectType.ReturnUnitOrSpy's own disambiguation (ReturnAnySpyCommand's
        // shape instead), told apart the same way ReturnAnySpyCommand.RequiresEnemyOnly already
        // does.
        private static bool IsGenuineReturnSpyState(MatchContext context)
        {
            var state = context.ActionSystem.CurrentState;
            if (state == ActionState.TargetingReturnSpy) return true;
            if (state != ActionState.SelectingSpyToReturn) return false;

            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect == null || pendingEffect.Type != EffectType.ReturnUnitOrSpy;
        }

        // "Funded by a card" is re-derived from ActionSystem's own trusted CurrentSourceEffect
        // (docs/coding-guidelines.md Rule #25) rather than trusted from a bare, caller-suppliable
        // non-empty CardId alone - EffectType.ReturnEnemySpy (Red Dragon) is the only card effect
        // that waives this command's Power cost; see GameEnums.cs's own doc comment on it.
        private static bool IsGenuinelyCardFunded(MatchContext context, string? cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.ReturnEnemySpy;
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == SiteId);
            if (site == null) return;

            // Affordability is checked BEFORE mutating the board (matching
            // ReturnAnySpyCommand.Execute's identical fix) - a directly-dispatched command with
            // 0 Power and no genuine card funding must never return a spy for free.
            bool isPaidByCard = IsGenuinelyCardFunded(context, CardId);
            if (!isPaidByCard && context.TurnManager.ActivePlayer.Power < GameConstants.ReturnSpyPowerCost)
            {
                context.ActionSystem.NotifyFailure($"Not enough Power to return spy! (Need {GameConstants.ReturnSpyPowerCost})");
                return;
            }

            if (context.MapManager.ReturnSpecificSpy(site, context.TurnManager.ActivePlayer, SpyColor))
            {
                if (!isPaidByCard)
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
