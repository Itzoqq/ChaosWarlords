using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MapManager
    {
        internal List<MapNode> Nodes { get; private set; }
        public List<Site> Sites { get; private set; }
        private readonly Dictionary<MapNode, Site> NodesiteLookup;

        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            Nodes = nodes;
            Sites = sites;
            NodesiteLookup = new Dictionary<MapNode, Site>();

            if (sites != null)
            {
                foreach (var site in sites)
                    foreach (var node in site.Nodes)
                        NodesiteLookup[node] = site;
            }
        }

        // --- Setup Methods ---
        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (Nodes.Count == 0) return;
            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(Nodes);
            Vector2 mapCenter = new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);
            Vector2 screenCenter = new(screenWidth / 2f, screenHeight / 2f);
            Vector2 offset = screenCenter - mapCenter;
            ApplyOffset(offset);
        }

        private void ApplyOffset(Vector2 offset)
        {
            foreach (var node in Nodes) node.Position += offset;
            if (Sites != null) foreach (var site in Sites) site.RecalculateBounds();
        }

        // --- Query Methods (Replaces Update Loop) ---
        public MapNode GetNodeAt(Vector2 position)
        {
            // Simple circular collision check against all nodes
            return Nodes.FirstOrDefault(n => Vector2.Distance(position, n.Position) <= MapNode.Radius);
        }

        public Site GetSiteAt(Vector2 position)
        {
            return Sites?.FirstOrDefault(s => s.Bounds.Contains(position));
        }

        public Site GetSiteForNode(MapNode node)
        {
            NodesiteLookup.TryGetValue(node, out Site site);
            return site;
        }

        // --- Logic Methods (Deploy, Control, etc.) ---
        // (Note: I changed GetHoveredNode to accept a specific node passed from the controller)
        public bool TryDeploy(Player currentPlayer, MapNode targetNode)
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

            // Optimization: Update only the specific site affected
            RecalculateSiteState(GetSiteForNode(node), player);

            if (player.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
        }

        /// <summary>
        /// OPTIMIZATION: Only recalculates control for a specific site when a relevant event occurs.
        /// Replaces the global UpdateSiteControl loop.
        /// </summary>
        public void RecalculateSiteState(Site site, Player activePlayer)
        {
            // If the action happened on a Route (not a Site), site will be null.
            // Troops on routes do not affect Site Control, so we can safely return.
            if (site == null) return;

            PlayerColor previousOwner = site.Owner;
            bool previousTotal = site.HasTotalControl;

            // 1. Determine Controller (Majority)
            PlayerColor newOwner = CalculateSiteOwner(site);

            // 2. Determine Total Control (All nodes owned by controller + No Enemy Spies)
            bool newTotalControl = CalculateTotalControl(site, newOwner);

            // 3. Apply State Changes
            site.Owner = newOwner;
            site.HasTotalControl = newTotalControl;

            // 4. Trigger Events / Logs / Rewards
            HandleControlChange(site, activePlayer, previousOwner, newOwner);
            HandleTotalControlChange(site, activePlayer, previousTotal, newTotalControl, newOwner);
        }

        private PlayerColor CalculateSiteOwner(Site site)
        {
            int redCount = site.Nodes.Count(n => n.Occupant == PlayerColor.Red);
            int blueCount = site.Nodes.Count(n => n.Occupant == PlayerColor.Blue);
            int neutralCount = site.Nodes.Count(n => n.Occupant == PlayerColor.Neutral);

            if (redCount > blueCount && redCount > neutralCount) return PlayerColor.Red;
            if (blueCount > redCount && blueCount > neutralCount) return PlayerColor.Blue;

            return PlayerColor.None;
        }

        private bool CalculateTotalControl(Site site, PlayerColor owner)
        {
            if (owner == PlayerColor.None) return false;
            bool ownsAllNodes = site.Nodes.All(n => n.Occupant == owner);
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
            if (Sites == null) return;
            foreach (var site in Sites)
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

            // Optimization: Update only the specific site affected
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

            // Optimization: Update only the specific site affected
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

                // Optimization: Spy placement directly affects Total Control
                RecalculateSiteState(site, player);
            }
            else
            {
                GameLogger.Log("No Spies left in supply!", LogChannel.Error);
            }
        }

        public bool ReturnSpy(Site site, Player activePlayer)
        {
            if (site.Nodes.Count > 0 && !HasPresence(site.Nodes[0], activePlayer.Color))
            {
                GameLogger.Log("Cannot return spy: No Presence at this Site!", LogChannel.Error);
                return false;
            }

            PlayerColor spyToRemove = site.Spies.FirstOrDefault(s => s != activePlayer.Color && s != PlayerColor.None);

            if (spyToRemove == PlayerColor.None)
            {
                GameLogger.Log("Invalid Target: No enemy spies at this Site.", LogChannel.Error);
                return false;
            }

            site.Spies.Remove(spyToRemove);
            GameLogger.Log($"Returned {spyToRemove} Spy from {site.Name} to barracks.", LogChannel.Combat);

            // Optimization: Removing a spy is a key trigger for gaining Total Control
            RecalculateSiteState(site, activePlayer);
            return true;
        }

        public void Supplant(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
            GameLogger.Log($"Supplanted enemy at Node {node.Id} (Added to Trophy Hall)", LogChannel.Combat);

            node.Occupant = attacker.Color;
            attacker.TroopsInBarracks--;

            // Optimization: Update only the specific site affected
            RecalculateSiteState(GetSiteForNode(node), attacker);
        }

        public bool CanAssassinate(MapNode target, Player attacker)
        {
            if (target.Occupant == PlayerColor.None) return false;
            if (target.Occupant == attacker.Color) return false;
            return HasPresence(target, attacker.Color);
        }

        public bool HasPresence(MapNode targetNode, PlayerColor player)
        {
            if (targetNode.Occupant == player) return true;
            Site parentSite = GetSiteForNode(targetNode);
            if (parentSite != null && parentSite.Spies.Contains(player)) return true;

            var nodesToCheck = parentSite != null ? parentSite.Nodes : new List<MapNode> { targetNode };
            return nodesToCheck.Any(n => n.Neighbors.Any(neighbor => neighbor.Occupant == player));
        }

        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            if (targetNode.Occupant != PlayerColor.None) return false;
            bool hasAnyTroops = Nodes.Any(n => n.Occupant == player);
            if (!hasAnyTroops) return true;
            return HasPresence(targetNode, player);
        }
    }
}