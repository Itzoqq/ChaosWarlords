using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Resolves CardEffect.DynamicAmountSource against live game state - shared by
    /// CardEffectProcessor (a SupportsRepeat effect's repeat COUNT, e.g. Quaggoth: "Assassinate
    /// one white troop for each site you control") and CardEffectApplier (a GainResource/
    /// DrawCard effect's resource/draw AMOUNT, e.g. White Dragon: "Gain 1 VP for every 2 sites
    /// you control"). Internal (not public) - this is a shared implementation detail between
    /// those 2 classes, not part of this assembly's public surface.
    /// </summary>
    internal static class DynamicAmountResolver
    {
        // Lookup-table dispatch (mirroring _effectHandlers'/ActionInputController's own
        // Dictionary<TKey, Func<...>> convention) rather than a switch over DynamicAmountSource -
        // each additional DynamicAmountSource case costs a switch's own cyclomatic complexity
        // another branch regardless of how it's written, where a dictionary lookup does not.
        private static readonly Dictionary<DynamicAmountSource, Func<MatchContext, int>> _dynamicAmountResolvers = new()
        {
            [DynamicAmountSource.SitesControlled] = ctx => ctx.MapManager.Sites.Count(s => s.Owner == ctx.ActivePlayer.Color),
            [DynamicAmountSource.TrophyHallCount] = ctx => ctx.ActivePlayer.TrophyHall,
            [DynamicAmountSource.PlayerTrophyHallCount] = ctx => ctx.ActivePlayer.TrophyHallByColor
                .Where(kv => kv.Key != PlayerColor.Neutral && kv.Key != PlayerColor.None)
                .Sum(kv => kv.Value),
            [DynamicAmountSource.InnerCircleCount] = ctx => ctx.ActivePlayer.InnerCircle.Count,
            [DynamicAmountSource.SpiesOnBoard] = ctx => ctx.MapManager.Sites.Count(s => s.HasSpy(ctx.ActivePlayer.Color)),
            [DynamicAmountSource.NeutralTrophyHallCount] = ctx => ctx.ActivePlayer.TrophyHallByColor.GetValueOrDefault(PlayerColor.Neutral),
            [DynamicAmountSource.SitesUnderTotalControl] = ctx => ctx.MapManager.Sites.Count(s => s.Owner == ctx.ActivePlayer.Color && s.HasTotalControl),
        };

        /// <summary>
        /// Returns effect.Amount unchanged for every effect (DynamicAmountSource.None, the
        /// default) - this is a no-op for every card that predates the dynamic-amount
        /// primitive. Otherwise computes the amount fresh from live game state at resolution
        /// time (e.g. White Dragon: "Gain 1 VP for every 2 sites you control", Beholder: "Gain
        /// Influence for every 3 troops in your trophy hall", Death Knight: "Gain 1 VP for every
        /// 5 player troops in your trophy hall", Vampire: "...gain 1 VP for every 3 cards in your
        /// inner circle", Aboleth: "Draw a card for each spy you have on the board", Black
        /// Dragon: "Gain 1 VP for every 3 white troops in your trophy hall", Red Dragon: "Gain 1
        /// VP for each site under your total control" - DynamicAmountDivisor is the "every N"
        /// part, integer division/floor. Effect-type-agnostic - consumed by GainResource/DrawCard
        /// as a resource/draw AMOUNT, and (see CardEffectProcessor.PushEffectContext's own
        /// dynamic-repeat-count gate) by any SupportsRepeat effect as a repeat COUNT instead
        /// (Quaggoth: "Assassinate one white troop for each site you control").
        /// </summary>
        public static int ResolveAmount(CardEffect effect, MatchContext context, IGameLogger logger)
        {
            if (effect.DynamicAmountSource == DynamicAmountSource.None)
                return effect.Amount;

            if (!_dynamicAmountResolvers.TryGetValue(effect.DynamicAmountSource, out var resolver))
            {
                // A new DynamicAmountSource enum value added without its matching resolver here
                // yet (e.g. mid-way through wiring up the next Dragon card) must not silently
                // resolve to a permanent, unexplained 0 - that's much harder to spot than a loud
                // warning in the log.
                logger.Log($"DynamicAmountResolver.ResolveAmount: no case wired for DynamicAmountSource.{effect.DynamicAmountSource} - resolving to 0.", LogChannel.Warning);
                return 0;
            }

            int count = resolver(context);

            if (effect.DynamicAmountDivisor <= 0)
            {
                // A non-positive divisor (e.g. a "0" typo in cards.json instead of the intended
                // "5") would otherwise be silently clamped to 1 below, granting up to N times
                // the intended amount with nothing in the log to distinguish it from a
                // legitimate divisor-1 card - just as easy to miss in playtesting as the unwired
                // DynamicAmountSource case above, so it gets the same loud warning treatment.
                logger.Log($"DynamicAmountResolver.ResolveAmount: DynamicAmountDivisor {effect.DynamicAmountDivisor} is not positive - clamping to 1.", LogChannel.Warning);
            }

            int divisor = Math.Max(1, effect.DynamicAmountDivisor);
            return count / divisor;
        }
    }
}
