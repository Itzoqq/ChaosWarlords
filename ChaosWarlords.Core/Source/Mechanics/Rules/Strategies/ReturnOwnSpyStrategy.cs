using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Returns one of the active player's OWN spies (e.g. Cloaker's "return one of your
    /// spies" half). Distinct from ReturnUnitStrategy (troops) and the enemy-spy-return flow
    /// (SpySubsystem.HandleReturnSpyInitialClick), which explicitly rejects the active
    /// player's own color.
    /// </summary>
    public class ReturnOwnSpyStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.ReturnOwnSpy;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingReturnOwnSpy;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return context.MapManager.Sites.Any(s => s.Spies.Contains(player.Color));
        }
    }
}
