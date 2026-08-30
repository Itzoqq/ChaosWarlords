using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    /// <summary>
    /// Represents a condition that must be met for a card effect to execute.
    /// Examples: "If you control a Site", "If you have 5+ Power"
    /// </summary>
    public class EffectCondition
    {
        public ConditionType Type { get; internal set; }
        public int Threshold { get; internal set; }
        public ResourceType? Resource { get; internal set; }

        public EffectCondition(ConditionType type, int threshold = 0, ResourceType? resource = null)
        {
            Type = type;
            Threshold = threshold;
            Resource = resource;
        }

        /// <summary>
        /// Evaluates whether the condition is met for the given player.
        /// </summary>
        public bool Evaluate(MatchContext context, Player player)
        {
            return Type switch
            {
                ConditionType.None => true,
                ConditionType.ControlsSite => EvaluateControlsSite(context, player),
                ConditionType.HasTroopsDeployed => EvaluateHasTroopsDeployed(context, player),
                ConditionType.HasResourceAmount => EvaluateHasResourceAmount(player),
                ConditionType.InnerCircleCount => player.InnerCircle.Count >= Threshold,
                ConditionType.HandSize => player.Hand.Count >= Threshold,
                _ => true
            };
        }

        private static bool EvaluateControlsSite(MatchContext context, Player player)
        {
            // Check if player controls any site (has troop on at least one node of a site)
            foreach (var site in context.MapManager.Sites)
            {
                if (site.NodesInternal.Any(node => node.Occupant == player.Color))
                    return true;
            }
            return false;
        }

        private static bool EvaluateHasTroopsDeployed(MatchContext context, Player player)
        {
            return context.MapManager.Nodes.Any(node => node.Occupant == player.Color);
        }

        private bool EvaluateHasResourceAmount(Player player)
        {
            if (Resource == null) return false;

            return Resource.Value switch
            {
                ResourceType.Power => player.Power >= Threshold,
                ResourceType.Influence => player.Influence >= Threshold,
                ResourceType.VictoryPoints => player.VictoryPoints >= Threshold,
                _ => false
            };
        }
    }
}
