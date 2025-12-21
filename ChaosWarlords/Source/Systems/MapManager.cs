using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MapManager : IMapManager
    {
        // Internal mutable lists used as backing fields
        public List<MapNode> NodesInternal { get; private set; }
        public List<Site> SitesInternal { get; private set; }

        // Explicitly implement the interface properties
        IReadOnlyList<MapNode> IMapManager.Nodes => NodesInternal;
        IReadOnlyList<Site> IMapManager.Sites => SitesInternal;

        private readonly Dictionary<MapNode, Site> NodesiteLookup;

        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            NodesInternal = nodes;
            SitesInternal = sites;
            NodesiteLookup = new Dictionary<MapNode, Site>();

            if (sites != null)
            {
                foreach (var site in sites)
                    foreach (var node in site.NodesInternal)
                        NodesiteLookup[node] = site;
            }
        }

        // --- Deadlock Prevention Implementations ---
        public bool HasValidAssassinationTarget(Player activePlayer)
        {
            // Valid if: Node has an occupant, it's NOT us, AND we have presence there.
            return NodesInternal.Any(n =>
                n.Occupant != PlayerColor.None &&
                n.Occupant != activePlayer.Color &&
                HasPresence(n, activePlayer.Color));
        }

        public bool HasValidReturnSpyTarget(Player activePlayer)
        {
            if (SitesInternal == null) return false;

            // Valid if: Site has our spy AND we have presence (Rules generally require presence to interact)
            // Note: We use the first node of the site to check presence for the site overall.
            return SitesInternal.Any(s =>
                s.Spies.Contains(activePlayer.Color) &&
                s.NodesInternal.Count > 0 &&
                HasPresence(s.NodesInternal[0], activePlayer.Color));
        }

        public bool HasValidPlaceSpyTarget(Player activePlayer)
        {
            if (SitesInternal == null) return false;

            // Removed HasPresence check. 
            // Rules state: "You don't need to have Presence at a site to place a spy there."
            return SitesInternal.Any(s =>
                !s.Spies.Contains(activePlayer.Color) &&
                s.NodesInternal.Count > 0);
        }
        // ------------------------------------------------

        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (NodesInternal.Count == 0) return;
            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(NodesInternal);
            Vector2 mapCenter = new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);
            Vector2 screenCenter = new(screenWidth / 2f, screenHeight / 2f);
            Vector2 offset = screenCenter - mapCenter;
            ApplyOffset(offset);
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

        public Site GetSiteForNode(MapNode node)
        {
            NodesiteLookup.TryGetValue(node, out Site site);
            return site;
        }

        public virtual bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            if (targetNode == null) return false;

            if (!CanDeployAt(targetNode, currentPlayer.Color))
            {
                GameLogger.Log($"Invalid Deployment at Node {targetNode.Id}: Occupied or No Presence.", LogChannel.Error);
                return false;
            }

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

            ExecuteDeploy(targetNode, currentPlayer);
            return true;
        }

        private void ExecuteDeploy(MapNode node, Player player)
        {
            player.Power -= 1;
            player.TroopsInBarracks--;
            node.Occupant = player.Color;

            GameLogger.Log($"Deployed Troop at Node {node.Id}. Supply: {player.TroopsInBarracks}", LogChannel.Combat);
            RecalculateSiteState(GetSiteForNode(node), player);

            if (player.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
        }

        public void RecalculateSiteState(Site site, Player activePlayer)
        {
            if (site == null) return;

            PlayerColor previousOwner = site.Owner;
            bool previousTotal = site.HasTotalControl;

            PlayerColor newOwner = CalculateSiteOwner(site);
            bool newTotalControl = CalculateTotalControl(site, newOwner);

            site.Owner = newOwner;
            site.HasTotalControl = newTotalControl;

            HandleControlChange(site, activePlayer, previousOwner, newOwner);
            HandleTotalControlChange(site, activePlayer, previousTotal, newTotalControl, newOwner);
        }

        private PlayerColor CalculateSiteOwner(Site site)
        {
            int redCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Red);
            int blueCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Blue);
            int neutralCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Neutral);

            if (redCount > blueCount && redCount > neutralCount) return PlayerColor.Red;
            if (blueCount > redCount && blueCount > neutralCount) return PlayerColor.Blue;

            return PlayerColor.None;
        }

        private bool CalculateTotalControl(Site site, PlayerColor owner)
        {
            if (owner == PlayerColor.None) return false;
            bool ownsAllNodes = site.NodesInternal.All(n => n.Occupant == owner);
            if (!ownsAllNodes) return false;
            bool hasEnemySpy = site.Spies.Any(spyColor => spyColor != owner && spyColor != PlayerColor.None);
            return !hasEnemySpy;
        }

        private void HandleControlChange(Site site, Player activePlayer, PlayerColor oldOwner, PlayerColor newOwner)
        {
            if (newOwner != oldOwner)
            {
                if (newOwner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    GameLogger.Log($"Seized Control of {site.Name}!", LogChannel.Economy);
                }
            }
        }

        private void HandleTotalControlChange(Site site, Player activePlayer, bool wasTotal, bool isTotal, PlayerColor owner)
        {
            if (isTotal != wasTotal)
            {
                if (isTotal && owner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                    GameLogger.Log($"Total Control established in {site.Name}!", LogChannel.Economy);
                }
                else if (!isTotal && wasTotal && activePlayer.Color == owner)
                {
                    GameLogger.Log($"Lost Total Control of {site.Name} (Spies or Troops lost).", LogChannel.Combat);
                }
            }
        }

        public void ApplyReward(Player player, ResourceType type, int amount)
        {
            if (type == ResourceType.Power) player.Power += amount;
            if (type == ResourceType.Influence) player.Influence += amount;
            if (type == ResourceType.VictoryPoints) player.VictoryPoints += amount;
        }

        public void DistributeControlRewards(Player activePlayer)
        {
            if (SitesInternal == null) return;
            foreach (var site in SitesInternal)
            {
                if (site.Owner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    if (site.HasTotalControl)
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                }
            }
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

        public bool HasValidMoveSource(Player activePlayer)
        {
            // Check if there is any node with an enemy troop where the player has presence 
            return NodesInternal.Any(n => CanMoveSource(n, activePlayer));
        }

        public bool CanMoveSource(MapNode node, Player activePlayer)
        {
            // Rule: Move an enemy troop from a space where you have Presence 
            bool isEnemy = node.Occupant != PlayerColor.None && node.Occupant != activePlayer.Color;
            return isEnemy && HasPresence(node, activePlayer.Color);
        }

        public bool CanMoveDestination(MapNode node)
        {
            // Rule: Destination must be an empty troop space [cite: 344]
            return node.Occupant == PlayerColor.None;
        }

        public void MoveTroop(MapNode source, MapNode destination)
        {
            if (source == null || destination == null) return;

            destination.Occupant = source.Occupant;
            source.Occupant = PlayerColor.None;

            GameLogger.Log($"Moved troop from {source.Id} to {destination.Id}.", LogChannel.Combat);
        }

        public bool CanAssassinate(MapNode target, Player attacker)
        {
            if (target.Occupant == PlayerColor.None) return false;
            if (target.Occupant == attacker.Color) return false;
            return HasPresence(target, attacker.Color);
        }

        public bool HasPresence(MapNode targetNode, PlayerColor player)
        {
            // 1. You have presence if you occupy the node itself
            if (targetNode.Occupant == player) return true;

            Site parentSite = GetSiteForNode(targetNode);

            // 2. You have presence if you have a SPY in the Site this node belongs to
            if (parentSite != null && parentSite.Spies.Contains(player)) return true;

            // 3. Adjacency Check
            // We check the neighbors of the target location.
            // If the target is a Site, we check neighbors of ALL nodes in the site.
            // If the target is a single node (like a Route), we check neighbors of just that node.
            var nodesToCheck = parentSite != null ? parentSite.NodesInternal : new List<MapNode> { targetNode };

            foreach (var checkNode in nodesToCheck)
            {
                foreach (var neighbor in checkNode.Neighbors)
                {
                    // Case A: The direct neighbor has your troop.
                    if (neighbor.Occupant == player) return true;

                    // Case B: The neighbor belongs to a Site where you have a troop (even if not at that specific node).
                    // This fixes the "City of Gold" adjacency issue.
                    Site neighborSite = GetSiteForNode(neighbor);
                    if (neighborSite != null && neighborSite.HasTroop(player))
                        return true;
                }
            }

            return false;
        }

        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            if (targetNode.Occupant != PlayerColor.None) return false;

            // If board is empty (or no friendly troops exist anywhere), first deployment is valid anywhere
            // (Strictly following the rule "if you have no troops... deploy anywhere")
            bool hasAnyTroops = NodesInternal.Any(n => n.Occupant == player);
            if (!hasAnyTroops) return true;

            return HasPresence(targetNode, player);
        }

        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer)
        {
            return site.Spies.Where(s => s != activePlayer.Color && s != PlayerColor.None).ToList();
        }

        public bool ReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor)
        {
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