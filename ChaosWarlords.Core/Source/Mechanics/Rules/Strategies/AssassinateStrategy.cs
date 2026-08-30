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

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingAssassinate;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return context.MapManager.HasValidAssassinationTarget(player);
        }
    }
}
