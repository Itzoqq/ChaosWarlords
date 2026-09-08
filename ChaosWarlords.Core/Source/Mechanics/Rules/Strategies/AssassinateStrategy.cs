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

            // ActionSystem.PendingSite must restrict this lookahead exactly the way
            // AssassinateCommand.Validate()'s own IsAtRequiredSite check already unconditionally
            // restricts the actual click - regardless of WHICH mechanism set it: Minotaur
            // Skeleton's own RestrictRepeatsToFirstTargetSite binding (after the first repeat),
            // or an earlier chain-link step like ReturnOwnSpyCommand (Cloaker). Previously only
            // honored for the RestrictRepeatsToFirstTargetSite case, which meant a chain-linked
            // (not self-repeat-bound) PendingSite could pass this global check yet still get
            // rejected by Validate(), stranding the player in an unclickable targeting state
            // with nothing to auto-resolve the effect as a clean "no valid target" instead (a
            // real, reproducible soft-lock, not just a theoretical gap - see RESOLVED.txt).
            // PendingSite is null whenever no chain scoping is active (cleared on every return
            // to Normal), so this is a no-op for every plain, unchained Assassinate effect.
            return context.MapManager.HasValidAssassinationTarget(player, effect?.TargetNeutralTroopOnly ?? false, restrictToSite: context.ActionSystem.PendingSite);
        }
    }
}
