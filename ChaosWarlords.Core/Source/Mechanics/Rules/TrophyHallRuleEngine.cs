using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Validity checks for EffectType.DeployFromTrophyHall (TROPHY-HALL-AS-TROOP-RESERVOIR,
    /// planning.txt) - "take a troop from a trophy hall and deploy it," sourced by
    /// Player.TrophyHallByColor rather than a site/node like every other check in
    /// MapRuleEngine, so it deliberately lives in its own engine rather than being bolted onto
    /// that map-focused one. Fully static, like MapRuleEngine.CanMoveDestination - no
    /// persistent state is needed since the full Player collection is passed in per call.
    ///
    /// Today only supports the "exactly one required color, e.g. Neutral/'white'" shape Mummy
    /// Lord needs (CardEffect.TargetNeutralTroopOnly). Lich ("take up to 2 troops, ANY color,
    /// from ONE specific opponent's hall - the one occupying the site where a spy was just
    /// placed") and Orcus ("take up to 2 troops, ANY color, from ANY trophy halls, mixed") are
    /// both genuinely different shapes - Lich's source player is externally determined (not
    /// resolved by this engine at all), and Orcus's "any color, any hall, up to 2" has no single
    /// "the sole eligible player" answer the way Mummy Lord's does. Deliberately not built here;
    /// extend this engine when one of those is actually tackled rather than guessing its shape
    /// now.
    /// </summary>
    public static class TrophyHallRuleEngine
    {
        /// <summary>
        /// True if EXACTLY ONE player currently has at least one eligible troop in their trophy
        /// hall - "eligible" meaning Neutral/"white" when <paramref name="requireNeutralOnly"/>
        /// is true (the only shape built today). Deliberately excludes the "2+ players
        /// simultaneously eligible" case, for the same underlying reason
        /// MapRuleEngine.HasValidReturnAnySpyTarget excludes an ambiguous site: no per-player
        /// click affordance exists in the Core input layer to ask "which player's trophy hall?"
        /// today, so an unresolvable-by-click ambiguity has to count as "no valid target" rather
        /// than opening a targeting state nothing can ever click into.
        ///
        /// Unlike that precedent, though, this is NOT a narrow/rare corner case and does NOT
        /// leave the action reachable some other way: HasValidReturnAnySpyTarget excludes
        /// ambiguity PER SITE, so one bad site just isn't offered while every other, unambiguous
        /// site still is - the action as a whole stays available. This check is global across
        /// ALL players with no such fallback: 2+ eligible players makes the WHOLE "take a troop
        /// from a trophy hall" option disappear for that round, and a single Mummy Lord play can
        /// trigger it against itself (its own Assassinate half adds a Neutral trophy to the
        /// active player's own hall, which can make a later round's DeployFromTrophyHall
        /// ambiguous against ANY other player who separately holds one) - see planning.txt and
        /// the tyrants-rules skill's bug-log.md for the full writeup and why a real fix (letting
        /// the player choose, e.g. by generalizing EffectType.SelectOpponent's existing
        /// "click a player" mechanism - which excludes the active player and drives a materially
        /// different chained-effect model, so isn't a drop-in reuse) is deferred rather than
        /// built here. See TryGetSoleEligibleSource for the same check plus the actual resolved
        /// answer.
        /// </summary>
        public static bool HasEligibleSource(IEnumerable<Player> players, bool requireNeutralOnly)
            => TryGetSoleEligibleSource(players, requireNeutralOnly, out _);

        /// <summary>
        /// Resolves the sole eligible source player's color when HasEligibleSource would return
        /// true, or PlayerColor.None (returns false) otherwise - covers both "nobody eligible"
        /// and "2+ players eligible" (see HasEligibleSource's doc comment for why both count as
        /// "no resolvable target" today).
        /// </summary>
        public static bool TryGetSoleEligibleSource(IEnumerable<Player> players, bool requireNeutralOnly, out PlayerColor sourcePlayerColor)
        {
            sourcePlayerColor = PlayerColor.None;

            if (!requireNeutralOnly)
            {
                // "Any color" sourcing (Lich/Orcus's shape) isn't built yet - see this class's
                // own doc comment. No shipped card reaches this branch.
                return false;
            }

            List<Player> eligible = players.Where(p => p.TrophyHallByColor.GetValueOrDefault(PlayerColor.Neutral) > 0).ToList();
            if (eligible.Count != 1)
            {
                return false;
            }

            sourcePlayerColor = eligible[0].Color;
            return true;
        }
    }
}
