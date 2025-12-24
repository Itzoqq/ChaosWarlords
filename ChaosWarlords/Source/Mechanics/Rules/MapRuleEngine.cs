using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MapRuleEngine
    {
        private readonly Dictionary<MapNode, Site> _nodeSiteLookup;
        private readonly List<MapNode> _nodes;
        private readonly List<Site> _sites;

        public MapRuleEngine(List<MapNode> nodes, List<Site> sites, Dictionary<MapNode, Site> lookup)
        {
            _nodes = nodes;
            _sites = sites;
            _nodeSiteLookup = lookup;
        }

        public Site GetSiteForNode(MapNode node)
        {
            if (node == null) return null;
            _nodeSiteLookup.TryGetValue(node, out Site site);
            return site;
        }

        // -------------------------------------------------------------------------
        // PRESENCE & VALIDATION LOGIC
        // -------------------------------------------------------------------------

        public bool HasPresence(MapNode targetNode, PlayerColor player)
        {
            if (targetNode == null) return false;

            if (targetNode.Occupant == player) return true;

            Site parentSite = GetSiteForNode(targetNode);
            if (HasSpyPresence(parentSite, player)) return true;

            return IsAdjacentToFriendly(targetNode, parentSite, player);
        }

        private bool HasSpyPresence(Site site, PlayerColor player)
        {
            return site != null && site.Spies.Contains(player);
        }

        private bool IsAdjacentToFriendly(MapNode targetNode, Site parentSite, PlayerColor player)
        {
            IEnumerable<MapNode> boundaryNodes = parentSite != null
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
            Site neighborSite = GetSiteForNode(neighbor);
            return neighborSite != null && neighborSite.HasTroop(player);
        }

        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            if (targetNode.Occupant != PlayerColor.None) return false;

            bool hasAnyTroops = _nodes.Any(n => n.Occupant == player);
            if (!hasAnyTroops) return true;

            return HasPresence(targetNode, player);
        }

        public bool CanAssassinate(MapNode target, Player attacker)
        {
            if (target.Occupant == PlayerColor.None) return false;
            if (target.Occupant == attacker.Color) return false;
            return HasPresence(target, attacker.Color);
        }

        public bool CanMoveSource(MapNode node, Player activePlayer)
        {
            bool isEnemy = node.Occupant != PlayerColor.None && node.Occupant != activePlayer.Color;
            return isEnemy && HasPresence(node, activePlayer.Color);
        }

        public bool CanMoveDestination(MapNode node)
        {
            return node.Occupant == PlayerColor.None;
        }

        // -------------------------------------------------------------------------
        // DEADLOCK PREVENTION CHECKS
        // -------------------------------------------------------------------------
        public bool HasValidAssassinationTarget(Player activePlayer)
        {
            return _nodes.Any(n =>
                n.Occupant != PlayerColor.None &&
                n.Occupant != activePlayer.Color &&
                HasPresence(n, activePlayer.Color));
        }

        public bool HasValidReturnSpyTarget(Player activePlayer)
        {
            if (_sites == null) return false;
            // Check for ANY spies (Validation logic allows returning any spy), not just own.
            // Also robust presence check using Any node.
            return _sites.Any(s =>
                s.Spies.Count > 0 &&
                s.NodesInternal.Any(n => HasPresence(n, activePlayer.Color)));
        }

        public bool HasValidReturnTroopTarget(Player activePlayer)
        {
            if (_nodes == null) return false;
            // Check for any node with a non-neutral troop where we have presence
            return _nodes.Any(n =>
                n.Occupant != PlayerColor.None &&
                n.Occupant != PlayerColor.Neutral &&
                HasPresence(n, activePlayer.Color));
        }

        public bool HasValidPlaceSpyTarget(Player activePlayer)
        {
            if (_sites == null) return false;
            return _sites.Any(s => !s.Spies.Contains(activePlayer.Color) && s.NodesInternal.Count > 0);
        }

        public bool HasValidMoveSource(Player activePlayer)
        {
            return _nodes.Any(n => CanMoveSource(n, activePlayer));
        }
    }
}