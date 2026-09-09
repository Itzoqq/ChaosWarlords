using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// "Return up to two troops or spies" (Intellect Devourer) - a target-type UNION: a single
    /// targeting step (ActionState.TargetingReturnUnitOrSpy) that accepts EITHER a node click
    /// (return that troop, via the existing ReturnTroopCommand/CanReturnTroop) OR a site click
    /// (return a spy there, via the new ReturnAnySpyCommand/CanReturnAnySpy) - both already
    /// symmetric between the active player's own unit (no Presence needed) and an enemy's
    /// (Presence needed). SupportsRepeat + CardEffect.AllowPartialRepeat together realize the
    /// "up to two" wording (Council Member's precedent), not a mandatory exact-2 like Deathblade.
    /// </summary>
    public class ReturnUnitOrSpyStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.ReturnUnitOrSpy;

        public bool IsTargetingEffect => true;

        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingReturnUnitOrSpy;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            // CardEffect.ReturnEnemyOnly (High Priest of Myrkul) restricts this lookahead the
            // same way it restricts the actual click - see AssassinateStrategy.HasValidTargets's
            // own doc comment for why re-deriving the effect from sourceCard is the established
            // pattern for a per-instance CardEffect flag a strategy isn't otherwise handed.
            var effect = sourceCard != null ? EffectTreeSearch.FindFirstEffect(sourceCard.Effects, EffectType.ReturnUnitOrSpy) : null;
            bool enemyOnly = effect?.ReturnEnemyOnly ?? false;

            return context.MapManager.HasValidReturnTroopTarget(player, enemyOnly)
                || context.MapManager.HasValidReturnAnySpyTarget(player, enemyOnly);
        }
    }
}
