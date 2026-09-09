using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// EffectType.DeployTroop (Gibbering Mouther: "Deploy 2 troops, then choose an opponent
    /// with a troop adjacent to at least 1 of them") - an immediate, stack-integrated Deploy
    /// distinct from GainResource(Troops)'s deferred PendingFreeTroops credit. See
    /// ActionState.TargetingDeployTroop's doc comment for why the two aren't unified.
    /// </summary>
    public class DeployTroopStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.DeployTroop;

        public bool IsTargetingEffect => true;

        // "Deploy 2 troops" - see IEffectStrategy.SupportsRepeat's doc comment. Each repeat's
        // own click-time check (CanDeployAt) is re-evaluated fresh by ActionInputController/
        // DeployTroopCommand.Validate for every click, same as Assassinate/PlaceSpy's repeats.
        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingDeployTroop;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return context.MapManager.HasValidDeployTarget(player.Color);
        }
    }
}
