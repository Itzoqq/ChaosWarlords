using System.Collections.Generic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Shared recursive lookup used by strategies whose HasValidTargets signature doesn't
    /// receive the CardEffect directly (AssassinateStrategy, SupplantStrategy, DevourStrategy,
    /// PromoteFromPileStrategy) and therefore have to re-locate their own EffectType on the
    /// source card's effect tree to read per-card targeting constraints (e.g.
    /// TargetNeutralTroopOnly).
    /// </summary>
    internal static class EffectTreeSearch
    {
        /// <summary>
        /// Searches for the first CardEffect matching <paramref name="type"/>, in this order:
        /// each top-level effect itself, then its full OnSuccess subtree, then its full
        /// Alternative subtree - first match wins.
        ///
        /// Search-order assumption: if a future card ever puts the SAME searched EffectType in
        /// both an effect's OnSuccess and its Alternative (with different constraints, e.g. two
        /// differently-scoped Supplant effects), this will silently prefer the OnSuccess one.
        /// No shipped card does this today, so it isn't fixed - just documented so it isn't
        /// accidental if it ever needs to change.
        /// </summary>
        public static CardEffect? FindFirstEffect(IEnumerable<CardEffect>? effects, EffectType type)
        {
            if (effects == null) return null;

            foreach (var effect in effects)
            {
                if (effect.Type == type) return effect;

                if (effect.OnSuccess != null)
                {
                    var found = FindFirstEffect(new[] { effect.OnSuccess }, type);
                    if (found != null) return found;
                }

                if (effect.Alternative != null)
                {
                    var found = FindFirstEffect(new[] { effect.Alternative }, type);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
