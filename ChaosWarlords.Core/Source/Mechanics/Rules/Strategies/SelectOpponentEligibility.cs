using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Shared eligibility check for EffectType.SelectOpponent, used by both
    /// SelectOpponentStrategy (the pre-click "is there ANY eligible opponent at all" lookahead)
    /// and SelectOpponentCommand.Validate() (defense-in-depth against the SPECIFIC opponent a
    /// forged command names) - kept in one place so the two can't silently diverge, the same
    /// reason AssassinateCommand/AssassinateStrategy share their PendingSite logic pattern
    /// (though those duplicate it; this extracts it, since a divergence here would be a real
    /// player-facing rules bug - accepting a click Validate() would then reject, or vice versa).
    ///
    /// Two eligibility modes: the default hand-size threshold (Cranium Rats: "an opponent with
    /// more than 3 cards in hand", CardEffect.Amount is the threshold) and
    /// CardEffect.RequiresAdjacencyToRecentDeploys (Gibbering Mouther: "an opponent with a troop
    /// adjacent to at least 1 of them [the just-deployed troops]", reading
    /// ActionSystem.PendingDeployedNodes).
    /// </summary>
    internal static class SelectOpponentEligibility
    {
        // Recursive (EffectTreeSearch), not a shallow top-level FirstOrDefault: Cranium Rats
        // has SelectOpponent as a top-level sibling effect, but Gibbering Mouther nests it as
        // DeployTroop's own OnSuccess ("Deploy 2 troops, THEN choose an opponent...") - a
        // shallow lookup would silently miss it and fall back to the wrong eligibility mode.
        public static CardEffect? FindEffect(Card? sourceCard) =>
            EffectTreeSearch.FindFirstEffect(sourceCard?.Effects, EffectType.SelectOpponent);

        public static bool IsEligible(MatchContext context, Player candidate, CardEffect? effect)
        {
            if (effect?.RequiresAdjacencyToRecentDeploys == true)
            {
                return context.ActionSystem.PendingDeployedNodes.Any(deployedNode =>
                    deployedNode.Neighbors.Any(neighbor => neighbor.Occupant == candidate.Color));
            }

            int threshold = effect?.Amount ?? 0;
            return candidate.Hand.Count > threshold;
        }

        /// <summary>
        /// Mode-specific diagnostic detail for SelectOpponentCommand.Validate()'s rejection
        /// message - kept alongside IsEligible so the two modes' wording can't drift out of
        /// sync with which check actually ran.
        /// </summary>
        public static string DescribeIneligibility(Player candidate, CardEffect? effect)
        {
            if (effect?.RequiresAdjacencyToRecentDeploys == true)
            {
                return "no troop adjacent to any recently-deployed node";
            }

            int threshold = effect?.Amount ?? 0;
            return $"doesn't meet the eligibility threshold (Hand.Count={candidate.Hand.Count} <= {threshold})";
        }
    }
}
