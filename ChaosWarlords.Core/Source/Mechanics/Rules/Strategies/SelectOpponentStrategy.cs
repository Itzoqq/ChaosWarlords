using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Generic "target a player" primitive - the active player chooses one opponent matching
    /// an eligibility rule (e.g. Cranium Rats' hand-size threshold, Gibbering Mouther's
    /// adjacency-to-recent-deploys check) - see SelectOpponentEligibility for both modes.
    /// </summary>
    public class SelectOpponentStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.SelectOpponent;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingOpponentSelect;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            var effect = SelectOpponentEligibility.FindEffect(sourceCard);
            return context.TurnManager.Players.Any(p => p != player && SelectOpponentEligibility.IsEligible(context, p, effect));
        }
    }
}
