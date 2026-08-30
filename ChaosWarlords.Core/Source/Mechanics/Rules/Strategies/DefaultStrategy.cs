using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class DefaultStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.None; // Helper usage, though this strategy handles many types

        public bool IsTargetingEffect => false;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.Normal;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            // Non-targeting effects are always valid target-wise (e.g. Draw Card, Gain Resource)
            return true;
        }
    }
}
