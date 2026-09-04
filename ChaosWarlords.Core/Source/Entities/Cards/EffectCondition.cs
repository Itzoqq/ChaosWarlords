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
        public SitePresenceType? PresenceType { get; internal set; }

        public EffectCondition(ConditionType type, int threshold = 0, ResourceType? resource = null, SitePresenceType? presenceType = null)
        {
            Type = type;
            Threshold = threshold;
            Resource = resource;
            PresenceType = presenceType;
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
                ConditionType.OpponentPresentAtSite => EvaluateOpponentPresentAtSite(context, player),
                _ => true
            };
        }

        /// <summary>
        /// "Another player has presence (spy or troop, per PresenceType) at
        /// ActionSystem.PendingSite" - e.g. Banshee's "if another player's spy is at that
        /// site" and Infiltrator's "if another player's troop is at that site". Reads
        /// PendingSite as the site just targeted by this effect's own PlaceSpy step (see
        /// PlaceSpyCommand.Execute/SpySubsystem.PerformPlaceSpy, which set it right before
        /// the OnSuccess chain resolves), not a later chained step's own site.
        /// </summary>
        private bool EvaluateOpponentPresentAtSite(MatchContext context, Player player)
        {
            var site = context.ActionSystem.PendingSite;
            if (site is null) return false;

            return PresenceType switch
            {
                SitePresenceType.Spy => site.Spies.Any(color => IsOpponentColor(color, player)),
                SitePresenceType.Troop => site.NodesInternal.Any(node => IsOpponentColor(node.Occupant, player)),
                _ => false
            };
        }

        /// <summary>
        /// True when <paramref name="color"/> belongs to a real opponent of
        /// <paramref name="player"/> - neither empty, Neutral (white/unaligned troops belong to
        /// no player), nor the player's own color.
        /// </summary>
        private static bool IsOpponentColor(PlayerColor color, Player player)
        {
            return color != PlayerColor.None && color != PlayerColor.Neutral && color != player.Color;
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
