using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class MoveUnitStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.MoveUnit;

        public bool IsTargetingEffect => true;

        // "Move up to 2 enemy troops" (Council Member) - see IEffectStrategy.SupportsRepeat's
        // doc comment. Safe for the existing single-move card (test_displacer, Amount=1):
        // PushEffectContext clamps RemainingRepeats to Math.Max(1, Amount), so
        // ShouldRepeatCurrentEffect's RemainingRepeats<=1 short-circuit still fires
        // immediately for it, completely unchanged.
        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingMoveSource;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return context.MapManager.HasValidMoveSource(player);
        }
    }
}
