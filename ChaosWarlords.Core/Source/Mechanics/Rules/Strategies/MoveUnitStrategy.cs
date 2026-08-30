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
