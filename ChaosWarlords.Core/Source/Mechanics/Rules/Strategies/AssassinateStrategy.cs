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
            var effect = sourceCard != null ? FindFirstEffect(sourceCard.Effects, EffectType.Assassinate) : null;
            return context.MapManager.HasValidAssassinationTarget(player, effect?.TargetNeutralTroopOnly ?? false);
        }

        // Same recursive-search pattern as DevourStrategy.FindFirstEffect - finds the
        // Assassinate CardEffect (including nested inside OnSuccess chains) so per-card
        // targeting constraints (e.g. TargetNeutralTroopOnly) can be read here.
        private static CardEffect? FindFirstEffect(IEnumerable<CardEffect> effects, EffectType type)
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
            }
            return null;
        }
    }
}
