using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class SupplantStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.Supplant;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingSupplant;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            // Supplant = Assassinate + Deploy. An empty barracks doesn't block it - the
            // deploy half grants 1 VP instead (rulebook p.12/22, same as the plain Deploy
            // action), so only the assassinate half's target requirement gates this.
            var effect = sourceCard != null ? FindFirstEffect(sourceCard.Effects, EffectType.Supplant) : null;
            return context.MapManager.HasValidAssassinationTarget(player, effect?.TargetNeutralTroopOnly ?? false);
        }

        // Same recursive-search pattern as DevourStrategy.FindFirstEffect - finds the
        // Supplant CardEffect (including nested inside OnSuccess chains) so per-card
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
