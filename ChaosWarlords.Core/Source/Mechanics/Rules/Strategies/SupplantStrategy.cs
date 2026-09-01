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
            return context.MapManager.HasValidAssassinationTarget(player);
        }
    }
}
