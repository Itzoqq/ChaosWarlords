using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class AssassinateStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.Assassinate;

        public bool IsTargetingEffect => true;

        // "Assassinate 2 troops" (Deathblade) - see IEffectStrategy.SupportsRepeat's doc
        // comment.
        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingAssassinate;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            var effect = sourceCard != null ? EffectTreeSearch.FindFirstEffect(sourceCard.Effects, EffectType.Assassinate) : null;

            // Minotaur Skeleton: once RestrictRepeatsToFirstTargetSite has bound
            // ActionSystem.PendingSite to the first repeat's site, "no more valid targets"
            // (ActionExecutionEngine.ShouldRepeatCurrentEffect's early-resolve fallback) must
            // mean "at that site", not "anywhere on the board" - see CardEffect.
            // RestrictRepeatsToFirstTargetSite's doc comment.
            var restrictToSite = (effect?.RestrictRepeatsToFirstTargetSite ?? false) ? context.ActionSystem.PendingSite : null;

            return context.MapManager.HasValidAssassinationTarget(player, effect?.TargetNeutralTroopOnly ?? false, restrictToSite: restrictToSite);
        }
    }
}
