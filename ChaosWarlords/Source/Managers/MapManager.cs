using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MapManager : IMapManager
    {
        // State
        public List<MapNode> NodesInternal { get; private set; }
        public List<Site> SitesInternal { get; private set; }
        private readonly Dictionary<MapNode, Site> _nodeSiteLookup;

        // Sub-Systems
        private readonly MapRuleEngine _ruleEngine;
        private readonly SiteControlSystem _controlSystem;

        // Interface Implementation
        IReadOnlyList<MapNode> IMapManager.Nodes => NodesInternal;
        IReadOnlyList<Site> IMapManager.Sites => SitesInternal;

        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            NodesInternal = nodes;
            SitesInternal = sites;
            _nodeSiteLookup = new Dictionary<MapNode, Site>();

            // Build Lookup
            if (sites != null)
            {
                foreach (var site in sites)
                    foreach (var node in site.NodesInternal)
                        _nodeSiteLookup[node] = site;
            }

            // Initialize Sub-Systems (Composition)
            // We pass the live lists/dictionary so the sub-systems always see current data
            _ruleEngine = new MapRuleEngine(NodesInternal, SitesInternal, _nodeSiteLookup);
            _controlSystem = new SiteControlSystem();
        }

        // -------------------------------------------------------------------------
        // FACADE METHODS (Delegating to Sub-Systems)
        // -------------------------------------------------------------------------

        public bool HasPresence(MapNode targetNode, PlayerColor player) => _ruleEngine.HasPresence(targetNode, player);
        public bool CanDeployAt(MapNode targetNode, PlayerColor player) => _ruleEngine.CanDeployAt(targetNode, player);
        public bool CanAssassinate(MapNode target, Player attacker) => _ruleEngine.CanAssassinate(target, attacker);
        public bool CanMoveSource(MapNode node, Player activePlayer) => _ruleEngine.CanMoveSource(node, activePlayer);
        public bool CanMoveDestination(MapNode node) => _ruleEngine.CanMoveDestination(node);

        public bool HasValidAssassinationTarget(Player activePlayer) => _ruleEngine.HasValidAssassinationTarget(activePlayer);
        public bool HasValidReturnSpyTarget(Player activePlayer) => _ruleEngine.HasValidReturnSpyTarget(activePlayer);
        public bool HasValidPlaceSpyTarget(Player activePlayer) => _ruleEngine.HasValidPlaceSpyTarget(activePlayer);
        public bool HasValidMoveSource(Player activePlayer) => _ruleEngine.HasValidMoveSource(activePlayer);

        public Site GetSiteForNode(MapNode node) => _ruleEngine.GetSiteForNode(node);

        public void RecalculateSiteState(Site site, Player activePlayer) => _controlSystem.RecalculateSiteState(site, activePlayer);
        public void DistributeControlRewards(Player activePlayer) => _controlSystem.DistributeControlRewards(SitesInternal, activePlayer);

        // -------------------------------------------------------------------------
        // NAVIGATION & QUERIES (Simple enough to keep here)
        // -------------------------------------------------------------------------

        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (NodesInternal.Count == 0) return;
            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(NodesInternal);
            Vector2 mapCenter = new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);
            Vector2 screenCenter = new(screenWidth / 2f, screenHeight / 2f);
            ApplyOffset(screenCenter - mapCenter);
        }

        private void ApplyOffset(Vector2 offset)
        {
            foreach (var node in NodesInternal) node.Position += offset;
            if (SitesInternal != null) foreach (var site in SitesInternal) site.RecalculateBounds();
        }

        public MapNode GetNodeAt(Vector2 position)
        {
            return NodesInternal.FirstOrDefault(n => Vector2.Distance(position, n.Position) <= MapNode.Radius);
        }

        public Site GetSiteAt(Vector2 position)
        {
            return SitesInternal?.FirstOrDefault(s => s.Bounds.Contains(position));
        }

        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer)
        {
            return site.Spies.Where(s => s != activePlayer.Color && s != PlayerColor.None).ToList();
        }

        // -------------------------------------------------------------------------
        // STATE MUTATION ACTIONS (Orchestration)
        // -------------------------------------------------------------------------

        public virtual bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            if (targetNode == null) return false;

            // Step 1: Validation (Delegated)
            if (!CanDeployAt(targetNode, currentPlayer.Color))
            {
                GameLogger.Log($"Invalid Deployment at Node {targetNode.Id}: Occupied or No Presence.", LogChannel.Error);
                return false;
            }

            // Step 2: Resource Checks (Business Logic)
            if (currentPlayer.TroopsInBarracks <= 0)
            {
                GameLogger.Log("Cannot Deploy: Barracks Empty!", LogChannel.Error);
                return false;
            }

            if (currentPlayer.Power < 1)
            {
                GameLogger.Log("Cannot Deploy: Not enough Power!", LogChannel.Economy);
                return false;
            }

            // Step 3: Execution
            ExecuteDeploy(targetNode, currentPlayer);
            return true;
        }

        private void ExecuteDeploy(MapNode node, Player player)
        {
            player.Power -= 1;
            player.TroopsInBarracks--;
            node.Occupant = player.Color;

            GameLogger.Log($"Deployed Troop at Node {node.Id}. Supply: {player.TroopsInBarracks}", LogChannel.Combat);

            // Step 4: React to State Change (Delegated)
            RecalculateSiteState(GetSiteForNode(node), player);

            if (player.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
        }

        public void Assassinate(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;

            GameLogger.Log($"Assassinated enemy at Node {node.Id}. Trophy Hall: {attacker.TrophyHall}", LogChannel.Combat);
            RecalculateSiteState(GetSiteForNode(node), attacker);
        }

        public void ReturnTroop(MapNode node, Player requestingPlayer)
        {
            if (node.Occupant == PlayerColor.Neutral) return;

            if (node.Occupant == requestingPlayer.Color)
            {
                node.Occupant = PlayerColor.None;
                requestingPlayer.TroopsInBarracks++;
                GameLogger.Log($"Returned friendly troop at Node {node.Id} to barracks.", LogChannel.Combat);
            }
            else if (node.Occupant != PlayerColor.None)
            {
                PlayerColor enemyColor = node.Occupant;
                node.Occupant = PlayerColor.None;
                GameLogger.Log($"Returned {enemyColor} troop at Node {node.Id} to their barracks.", LogChannel.Combat);
            }

            RecalculateSiteState(GetSiteForNode(node), requestingPlayer);
        }

        public void PlaceSpy(Site site, Player player)
        {
            if (site.Spies.Contains(player.Color))
            {
                GameLogger.Log("You already have a spy at this site.", LogChannel.Error);
                return;
            }

            if (player.SpiesInBarracks > 0)
            {
                player.SpiesInBarracks--;
                site.Spies.Add(player.Color);
                GameLogger.Log($"Spy placed at {site.Name}.", LogChannel.Combat);
                RecalculateSiteState(site, player);
            }
            else
            {
                GameLogger.Log("No Spies left in supply!", LogChannel.Error);
            }
        }

        public void Supplant(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
            GameLogger.Log($"Supplanted enemy at Node {node.Id} (Added to Trophy Hall)", LogChannel.Combat);

            node.Occupant = attacker.Color;
            attacker.TroopsInBarracks--;

            RecalculateSiteState(GetSiteForNode(node), attacker);
        }

        public void MoveTroop(MapNode source, MapNode destination)
        {
            if (source == null || destination == null) return;

            destination.Occupant = source.Occupant;
            source.Occupant = PlayerColor.None;

            GameLogger.Log($"Moved troop from {source.Id} to {destination.Id}.", LogChannel.Combat);
            // Note: Site state might change for both source and destination sites
            RecalculateSiteState(GetSiteForNode(source), null); // null player because this is just cleanup
            RecalculateSiteState(GetSiteForNode(destination), null);
        }

        public bool ReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor)
        {
            // Use local check via engine to ensure consistency
            if (site.NodesInternal.Count > 0 && !HasPresence(site.NodesInternal[0], activePlayer.Color))
            {
                GameLogger.Log("Cannot return spy: No Presence at this Site!", LogChannel.Error);
                return false;
            }

            if (!site.Spies.Contains(targetSpyColor) || targetSpyColor == activePlayer.Color)
            {
                GameLogger.Log($"Invalid Target: {targetSpyColor} spy not found here.", LogChannel.Error);
                return false;
            }

            site.Spies.Remove(targetSpyColor);
            GameLogger.Log($"Returned {targetSpyColor} Spy from {site.Name} to barracks.", LogChannel.Combat);
            RecalculateSiteState(site, activePlayer);
            return true;
        }
    }
}