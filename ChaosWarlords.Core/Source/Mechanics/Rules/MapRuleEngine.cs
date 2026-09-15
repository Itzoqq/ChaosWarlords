using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    public class MapRuleEngine
    {
        private readonly Dictionary<MapNode, Site> _nodeSiteLookup;
        private readonly List<MapNode> _nodes;
        private readonly List<Site> _sites;
        private readonly IGameLogger _logger;

        public MapRuleEngine(List<MapNode> nodes, List<Site> sites, Dictionary<MapNode, Site> lookup, IGameLogger logger)
        {
            _nodes = nodes;
            _sites = sites;
            _nodeSiteLookup = lookup;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Site? GetSiteForNode(MapNode node)
        {
            if (node is null) return null;
            _nodeSiteLookup.TryGetValue(node, out var site);
            return site;
        }

        // -------------------------------------------------------------------------
        // PRESENCE & VALIDATION LOGIC
        // -------------------------------------------------------------------------

        public bool HasPresence(MapNode targetNode, PlayerColor player)
        {
            if (targetNode is null) return false;

            if (targetNode.Occupant == player) return true;

            Site? parentSite = GetSiteForNode(targetNode);
            if (HasSpyPresence(parentSite, player)) return true;

            return IsAdjacentToFriendly(targetNode, parentSite, player);
        }

        private static bool HasSpyPresence(Site? site, PlayerColor player)
        {
            return site is not null && site.Spies.Contains(player);
        }

        private bool IsAdjacentToFriendly(MapNode targetNode, Site? parentSite, PlayerColor player)
        {
            IEnumerable<MapNode> boundaryNodes = parentSite is not null
                ? parentSite.NodesInternal
                : Enumerable.Repeat(targetNode, 1);

            return boundaryNodes.Any(node => HasFriendlyNeighbor(node, player));
        }

        private bool HasFriendlyNeighbor(MapNode node, PlayerColor player)
        {
            return node.Neighbors.Any(neighbor => IsSourceOfPresence(neighbor, player));
        }

        private bool IsSourceOfPresence(MapNode neighbor, PlayerColor player)
        {
            if (neighbor.Occupant == player) return true;
            Site? neighborSite = GetSiteForNode(neighbor);
            return neighborSite is not null && neighborSite.HasTroop(player);
        }

        public MatchPhase CurrentPhase { get; set; } = MatchPhase.Setup;

        public void SetPhase(MatchPhase phase)
        {
            CurrentPhase = phase;
        }

        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            ArgumentNullException.ThrowIfNull(targetNode);
            if (targetNode.Occupant != PlayerColor.None) return false;

            return CurrentPhase == MatchPhase.Setup
                ? CanDeployDuringSetup(targetNode, player)
                : CanDeployDuringPlay(targetNode, player);
        }

        private bool CanDeployDuringSetup(MapNode targetNode, PlayerColor player)
        {
            var site = GetSiteForNode(targetNode);
            if (site is not StartingSite)
            {
                _logger.Log($"SetupDeploy Fail: {site?.Name} is {site?.GetType().Name}, not StartingSite.", LogChannel.Error);
                return false;
            }

            if (SiteOccupiedByOtherPlayer(site, player))
            {
                _logger.Log($"SetupDeploy Fail: {site.Name} already occupied by another player.", LogChannel.Error);
                return false;
            }

            // Player must have NO presence on map (first unit logic)
            return !PlayerHasPresenceOnMap(player);
        }

        private bool CanDeployDuringPlay(MapNode targetNode, PlayerColor player)
        {
            // If player has no presence, can deploy anywhere (eliminated - can deploy anywhere)
            if (!PlayerHasPresenceOnMap(player)) return true;

            // Otherwise, must have presence at target node
            return HasPresence(targetNode, player);
        }

        private bool PlayerHasPresenceOnMap(PlayerColor player)
        {
            bool hasTroops = _nodes.Any(n => n.Occupant == player);
            bool hasSpies = _sites.Any(s => s.Spies.Contains(player));
            return hasTroops || hasSpies;
        }

        private static bool SiteOccupiedByOtherPlayer(Site site, PlayerColor player)
        {
            return site.NodesInternal.Any(n => n.Occupant != PlayerColor.None && n.Occupant != player);
        }

        public bool CanAssassinate(MapNode target, Player attacker, bool requireNeutralTroop = false, bool ignoresPresence = false)
        {
            if (target.Occupant == PlayerColor.None) return false;
            if (target.Occupant == attacker.Color) return false;
            if (requireNeutralTroop && target.Occupant != PlayerColor.Neutral) return false;
            if (ignoresPresence) return true;
            return HasPresence(target, attacker.Color);
        }

        public bool CanMoveSource(MapNode node, Player activePlayer)
        {
            bool isEnemy = node.Occupant != PlayerColor.None && node.Occupant != activePlayer.Color;
            return isEnemy && HasPresence(node, activePlayer.Color);
        }

        public static bool CanMoveDestination(MapNode node)
        {
            return node.Occupant == PlayerColor.None;
        }

        // -------------------------------------------------------------------------
        // DEADLOCK PREVENTION CHECKS
        // -------------------------------------------------------------------------
        public bool HasValidAssassinationTarget(Player activePlayer, bool requireNeutralTroop = false, bool ignoresPresence = false, Site? restrictToSite = null)
        {
            return _nodes.Any(n =>
                n.Occupant != PlayerColor.None &&
                n.Occupant != activePlayer.Color &&
                (!requireNeutralTroop || n.Occupant == PlayerColor.Neutral) &&
                (restrictToSite == null || restrictToSite.NodesInternal.Contains(n)) &&
                (ignoresPresence || HasPresence(n, activePlayer.Color)));
        }

        // EffectType.ReturnEnemySpy (Red Dragon); also the base "Return an enemy spy" action's
        // own pre-check (TryStartReturnSpy) as of planning.txt TIER 1 item 16, which retired a
        // looser HasValidReturnSpyTarget that used to accept ANY spy present, including the
        // active player's own - this must positively confirm an ENEMY spy is present before
        // targeting opens, or a site with only the active player's own spy there would
        // incorrectly look like a valid target and then reject every click.
        public bool HasValidReturnEnemySpyTarget(Player activePlayer)
        {
            return _sites?.Any(s =>
                s.Spies.Any(c => c != activePlayer.Color) &&
                s.NodesInternal.Any(n => HasPresence(n, activePlayer.Color))) ?? false;
        }

        // EffectType.ReturnUnitOrSpy's spy-side "no more legal targets" check - true whenever
        // AT LEAST ONE eligible spy exists at any site, matching HasValidReturnTroopTarget's own
        // "any eligible target somewhere" shape. A site with 2+ simultaneously eligible spies is
        // resolvable via SpySubsystem.HandleReturnUnitOrSpySite's disambiguation sub-state
        // (reusing SelectingSpyToReturn - see planning.txt TIER 1 item 16), so it no longer needs
        // to be excluded here to avoid a soft-lock. enemyOnly (High Priest of Myrkul: "Return
        // ANOTHER PLAYER'S troop or spy") excludes the active player's own spies from
        // eligibility entirely - see CardEffect.ReturnEnemyOnly.
        public bool HasValidReturnAnySpyTarget(Player activePlayer, bool enemyOnly = false)
        {
            return _sites?.Any(s => s.Spies.Any(color => IsSpyReturnEligible(s, activePlayer, color, enemyOnly))) ?? false;
        }

        // enemyOnly excludes the active player's own spy from eligibility entirely; otherwise
        // an own spy is always eligible (no Presence needed) and an enemy spy needs Presence
        // at the site - see HasExactlyOneReturnableSpyAt's own doc comment.
        private bool IsSpyReturnEligible(Site site, Player activePlayer, PlayerColor spyColor, bool enemyOnly)
        {
            bool isOwnSpy = spyColor == activePlayer.Color;
            if (enemyOnly && isOwnSpy) return false;
            if (!enemyOnly && isOwnSpy) return true;

            return site.NodesInternal.Any(n => HasPresence(n, activePlayer.Color));
        }

        // enemyOnly (High Priest of Myrkul) excludes the active player's own troops from
        // eligibility entirely - see CardEffect.ReturnEnemyOnly.
        public bool HasValidReturnTroopTarget(Player activePlayer, bool enemyOnly = false)
        {
            // See MapManager.CanReturnTroop's comment: Presence is only required to return
            // an ENEMY troop, not the player's own - matching that check here too, or a
            // player with only their own troops on the board (and no enemy Presence
            // anywhere) would see "Return a Troop" card effects report no valid targets at
            // all, even though they could legally return one of their own troops from
            // anywhere. See planning.txt.
            return _nodes?.Any(n =>
                n.Occupant != PlayerColor.None &&
                n.Occupant != PlayerColor.Neutral &&
                (enemyOnly
                    ? n.Occupant != activePlayer.Color && HasPresence(n, activePlayer.Color)
                    : (n.Occupant == activePlayer.Color || HasPresence(n, activePlayer.Color)))) ?? false;
        }

        public bool HasValidPlaceSpyTarget(Player activePlayer)
        {
            if (_sites is null) return false;
            return _sites.Any(s => !s.Spies.Contains(activePlayer.Color) && s.NodesInternal.Count > 0);
        }

        // Place a Spy's own empty-barracks exception (p.12) - is there a site to return from at
        // all? Mirrors ReturnOwnSpyStrategy.HasValidTargets' identical check.
        public bool HasOwnSpyOnBoard(Player activePlayer)
        {
            return _sites?.Any(s => s.Spies.Contains(activePlayer.Color)) ?? false;
        }

        public bool HasValidMoveSource(Player activePlayer)
        {
            return _nodes.Any(n => CanMoveSource(n, activePlayer));
        }

        // EffectType.DeployTroop (Gibbering Mouther) - is there ANY empty node CanDeployAt
        // would accept for this player right now (honoring the same "or anywhere, if you have
        // zero Presence on the board" rule the basic Deploy action already gets)?
        public bool HasValidDeployTarget(PlayerColor player)
        {
            return _nodes.Any(n => CanDeployAt(n, player));
        }
    }
}


