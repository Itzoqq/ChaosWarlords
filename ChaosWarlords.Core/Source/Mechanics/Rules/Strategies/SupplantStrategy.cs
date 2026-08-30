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
            // Supplant requires Assassinate target + placing troop
            return player.TroopsInBarracks > 0 && context.MapManager.HasValidAssassinationTarget(player);
        }
    }
}
