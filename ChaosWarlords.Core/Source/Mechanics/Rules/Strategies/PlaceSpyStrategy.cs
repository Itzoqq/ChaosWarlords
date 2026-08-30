using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class PlaceSpyStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.PlaceSpy;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingPlaceSpy;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return player.SpiesInBarracks > 0 && context.MapManager.HasValidPlaceSpyTarget(player);
        }
    }
}
