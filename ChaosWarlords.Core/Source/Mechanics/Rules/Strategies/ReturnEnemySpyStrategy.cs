using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Returns an ENEMY spy (Red Dragon's "Return an enemy spy") - reuses the same
    /// ActionState.TargetingReturnSpy/SpySubsystem.HandleReturnSpyInitialClick/ResolveSpyCommand
    /// machinery the paid basic action already uses (ResolveSpyCommand already waives its Power
    /// cost whenever CardId is set), including that flow's own multi-enemy-spy disambiguation
    /// sub-step. Distinct from ReturnOwnSpyStrategy (the active player's own spy only) and
    /// ReturnUnitOrSpyStrategy (troop-or-spy, own-or-enemy - too broad for "an enemy spy").
    /// </summary>
    public class ReturnEnemySpyStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.ReturnEnemySpy;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingReturnSpy;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return context.MapManager.HasValidReturnEnemySpyTarget(player);
        }
    }
}
