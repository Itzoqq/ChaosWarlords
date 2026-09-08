using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// "Take a white troop from any trophy hall and deploy it anywhere on the board" (Mummy
    /// Lord) - a single node-click destination step (ActionState.
    /// TargetingDeployFromTrophyHall), mirroring MoveUnit's destination half ("any empty troop
    /// space, no Presence needed"). WHICH player's trophy hall to draw from is resolved by
    /// TrophyHallRuleEngine before targeting ever opens (see CardEffectProcessor.
    /// ApplyDeployFromTrophyHall) - this strategy's own HasValidTargets requires that
    /// resolution to be unambiguous (exactly one eligible player) AND at least one empty node to
    /// exist, so "no valid target" here means the card option genuinely can't be offered right
    /// now, not just "needs a click to disambiguate."
    /// </summary>
    public class DeployFromTrophyHallStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.DeployFromTrophyHall;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingDeployFromTrophyHall;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            var effect = sourceCard != null ? EffectTreeSearch.FindFirstEffect(sourceCard.Effects, EffectType.DeployFromTrophyHall) : null;
            bool requireNeutralOnly = effect?.TargetNeutralTroopOnly ?? false;

            return TrophyHallRuleEngine.HasEligibleSource(context.TurnManager.Players, requireNeutralOnly)
                && context.MapManager.Nodes.Any(context.MapManager.CanMoveDestination);
        }
    }
}
