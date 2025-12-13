using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MapManager
    {
        private List<MapNode> _nodes;
        public List<Site> Sites { get; private set; }
        private readonly Dictionary<MapNode, Site> _nodeSiteLookup;
        public Texture2D PixelTexture { get; set; }

        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            _nodes = nodes;
            Sites = sites;
            _nodeSiteLookup = [];

            if (sites != null)
            {
                foreach (var site in sites)
                    foreach (var node in site.Nodes)
                        _nodeSiteLookup[node] = site;
            }
        }

        // --- Visual / Setup Methods (Low Complexity) ---
        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (_nodes.Count == 0) return;

            // 1. Calculate Bounds
            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(_nodes);

            // 2. Calculate Offset required to center
            Vector2 mapCenter = new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);
            Vector2 screenCenter = new(screenWidth / 2f, screenHeight / 2f);
            Vector2 offset = screenCenter - mapCenter;

            // 3. Apply
            ApplyOffset(offset);
        }

        private (float MinX, float MinY, float MaxX, float MaxY) CalculateMapBounds()
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in _nodes)
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            return (minX, minY, maxX, maxY);
        }

        private void ApplyOffset(Vector2 offset)
        {
            foreach (var node in _nodes)
            {
                node.Position += offset;
            }

            if (Sites != null)
            {
                foreach (var site in Sites) site.RecalculateBounds();
            }
        }

        public void Update(MouseState mouseState)
        {
            foreach (var node in _nodes) node.Update(mouseState);
        }

        public MapNode GetHoveredNode() => _nodes.FirstOrDefault(n => n.IsHovered);
        public Site GetHoveredSite(Vector2 mousePos) => Sites?.FirstOrDefault(s => s.Bounds.Contains(mousePos));

        // --- REFACTORED: Deployment Logic ---
        public bool TryDeploy(Player currentPlayer)
        {
            var hoveredNode = GetHoveredNode();

            // 1. Validation (Guard Clauses reduce nesting)
            if (hoveredNode == null) return false;

            if (!CanDeployAt(hoveredNode, currentPlayer.Color))
            {
                GameLogger.Log($"Invalid Deployment at Node {hoveredNode.Id}: Occupied or No Presence.", LogChannel.Error);
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

            // 2. Execution
            ExecuteDeploy(hoveredNode, currentPlayer);
            return true;
        }

        private void ExecuteDeploy(MapNode node, Player player)
        {
            player.Power -= 1;
            player.TroopsInBarracks--;
            node.Occupant = player.Color;

            GameLogger.Log($"Deployed Troop at Node {node.Id}. Supply: {player.TroopsInBarracks}", LogChannel.Combat);

            UpdateSiteControl(player); // Recalculate control after placement

            if (player.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
        }

        // --- REFACTORED: Site Control Logic (Complexity Reduction) ---
        // Originally one massive method, now broken into 4 logical steps.

        public void UpdateSiteControl(Player activePlayer)
        {
            if (Sites == null) return;

            foreach (var site in Sites)
            {
                // Snapshot previous state
                PlayerColor previousOwner = site.Owner;
                bool previousTotal = site.HasTotalControl;

                // 1. Calculate New State
                PlayerColor newOwner = CalculateSiteOwner(site);
                bool newTotalControl = CalculateTotalControl(site, newOwner);

                // 2. Apply Changes
                site.Owner = newOwner;
                site.HasTotalControl = newTotalControl;

                // 3. Trigger Side Effects (Rewards/Logs)
                HandleControlChange(site, activePlayer, previousOwner, newOwner);
                HandleTotalControlChange(site, activePlayer, previousTotal, newTotalControl, newOwner);
            }
        }

        // Extracted Logic: Who owns this site? (Majority Rule)
        private PlayerColor CalculateSiteOwner(Site site)
        {
            int redCount = site.Nodes.Count(n => n.Occupant == PlayerColor.Red);
            int blueCount = site.Nodes.Count(n => n.Occupant == PlayerColor.Blue);
            int neutralCount = site.Nodes.Count(n => n.Occupant == PlayerColor.Neutral);

            // Tyrants Rule: You must have MORE troops than any other SINGLE faction[cite: 1382].
            // Ties mean NO ONE controls it.
            if (redCount > blueCount && redCount > neutralCount) return PlayerColor.Red;
            if (blueCount > redCount && blueCount > neutralCount) return PlayerColor.Blue;

            return PlayerColor.None;
        }

        // Extracted Logic: Is there Total Control?
        private bool CalculateTotalControl(Site site, PlayerColor owner)
        {
            if (owner == PlayerColor.None) return false;

            // Rule 1: All nodes must be occupied by the owner [cite: 1383]
            bool ownsAllNodes = site.Nodes.All(n => n.Occupant == owner);
            if (!ownsAllNodes) return false;

            // Rule 2: No enemy spies present [cite: 1383]
            // (Assumes 2 player game for simplicity, logic holds for any enemy)
            bool hasEnemySpy = site.Spies.Any(spyColor => spyColor != owner && spyColor != PlayerColor.None);

            return !hasEnemySpy;
        }

        // Extracted Logic: Handle rewards when ownership flips
        private void HandleControlChange(Site site, Player activePlayer, PlayerColor oldOwner, PlayerColor newOwner)
        {
            if (newOwner != oldOwner)
            {
                // Only give reward if the active player triggered the change and it is a City
                if (newOwner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    GameLogger.Log($"Seized Control of {site.Name}!", LogChannel.Economy);
                }
            }
        }

        // Extracted Logic: Handle Total Control rewards
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

        // --- Standard Actions (Low Complexity) ---

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
                // Logic: Only apply rewards for sites owned by the active player
                if (site.Owner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);

                    if (site.HasTotalControl)
                    {
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                    }
                }
            }
        }

        public void Assassinate(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
            GameLogger.Log($"Assassinated enemy at Node {node.Id}. Trophy Hall: {attacker.TrophyHall}", LogChannel.Combat);

            UpdateSiteControl(attacker);
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

            UpdateSiteControl(requestingPlayer);
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
                UpdateSiteControl(player);
            }
            else
            {
                GameLogger.Log("No Spies left in supply!", LogChannel.Error);
            }
        }

        public bool ReturnSpy(Site site, Player activePlayer)
        {
            // Presence check optimization
            if (site.Nodes.Count > 0 && !HasPresence(site.Nodes[0], activePlayer.Color))
            {
                GameLogger.Log("Cannot return spy: No Presence at this Site!", LogChannel.Error);
                return false;
            }

            // Find valid target
            PlayerColor spyToRemove = site.Spies.FirstOrDefault(s => s != activePlayer.Color && s != PlayerColor.None);

            if (spyToRemove == PlayerColor.None) // Default struct value
            {
                GameLogger.Log("Invalid Target: No enemy spies at this Site.", LogChannel.Error);
                return false;
            }

            site.Spies.Remove(spyToRemove);
            GameLogger.Log($"Returned {spyToRemove} Spy from {site.Name} to barracks.", LogChannel.Combat);
            UpdateSiteControl(activePlayer);
            return true;
        }

        public void Supplant(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            // Assassinate
            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
            GameLogger.Log($"Supplanted enemy at Node {node.Id} (Added to Trophy Hall)", LogChannel.Combat);

            // Deploy
            node.Occupant = attacker.Color;
            attacker.TroopsInBarracks--;

            UpdateSiteControl(attacker);
        }

        // --- Validation Helpers (Low Complexity) ---

        public bool CanAssassinate(MapNode target, Player attacker)
        {
            if (target.Occupant == PlayerColor.None) return false;
            if (target.Occupant == attacker.Color) return false;
            return HasPresence(target, attacker.Color);
        }

        public bool HasPresence(MapNode targetNode, PlayerColor player)
        {
            // Rule: A player has presence at a location if they have a troop there,
            // a troop at an adjacent location, or a spy at the site containing the location.

            // 0. Direct occupation
            if (targetNode.Occupant == player) return true;

            // 1. Spies
            Site parentSite = GetSiteForNode(targetNode);
            if (parentSite != null && parentSite.Spies.Contains(player)) return true;

            // 2. Adjacency (to the node itself or any node in its site)
            var nodesToCheck = parentSite != null ? parentSite.Nodes : new List<MapNode> { targetNode };

            // Check if ANY neighbor of ANY relevant node is occupied by us
            return nodesToCheck.Any(n => n.Neighbors.Any(neighbor => neighbor.Occupant == player));
        }

        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            if (targetNode.Occupant != PlayerColor.None) return false;

            // "Start of Game" Rule check
            bool hasAnyTroops = _nodes.Any(n => n.Occupant == player);
            if (!hasAnyTroops) return true;

            return HasPresence(targetNode, player);
        }

        // --- Helpers ---
        private Site GetSiteForNode(MapNode node)
        {
            _nodeSiteLookup.TryGetValue(node, out Site site);
            return site;
        }

        // Keeping Draw-related math helpers private (unchanged)
        private Vector2 GetIntersection(Rectangle rect, Vector2 start, Vector2 end)
        {
            Vector2[] corners =
            [
                new Vector2(rect.Left, rect.Top),
                new Vector2(rect.Right, rect.Top),
                new Vector2(rect.Right, rect.Bottom),
                new Vector2(rect.Left, rect.Bottom)
            ];

            float closestDist = float.MaxValue;
            Vector2 closestPoint = end;

            for (int i = 0; i < 4; i++)
            {
                Vector2 p1 = corners[i];
                Vector2 p2 = corners[(i + 1) % 4];

                if (MapGeometry.TryGetLineIntersection(start, end, p1, p2, out Vector2 intersection))
                {
                    float d = Vector2.DistanceSquared(start, intersection);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestPoint = intersection;
                    }
                }
            }
            return closestPoint;
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
        {
            if (PixelTexture == null) return;

            Vector2 edge = end - start;
            float angle = (float)System.Math.Atan2(edge.Y, edge.X);

            spriteBatch.Draw(PixelTexture,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            DrawSites(spriteBatch, font);
            DrawRoutes(spriteBatch);
            DrawNodes(spriteBatch);
        }

        private void DrawSites(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (Sites == null) return;
            foreach (var site in Sites)
            {
                site.Draw(spriteBatch, font, PixelTexture);
            }
        }

        private void DrawNodes(SpriteBatch spriteBatch)
        {
            foreach (var node in _nodes)
            {
                node.Draw(spriteBatch);
            }
        }

        private void DrawRoutes(SpriteBatch spriteBatch)
        {
            foreach (var node in _nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    // Optimization: Only draw connection once per pair
                    if (node.Id < neighbor.Id)
                    {
                        DrawSingleRoute(spriteBatch, node, neighbor);
                    }
                }
            }
        }

        private void DrawSingleRoute(SpriteBatch spriteBatch, MapNode node, MapNode neighbor)
        {
            Site startSite = GetSiteForNode(node);
            Site endSite = GetSiteForNode(neighbor);

            // Visual Clutter Rule: Don't draw lines strictly inside a single site
            if (startSite != null && startSite == endSite) return;

            Vector2 p1 = startSite != null ? startSite.Bounds.Center.ToVector2() : node.Position;
            Vector2 p2 = endSite != null ? endSite.Bounds.Center.ToVector2() : neighbor.Position;

            // Geometry: Clip lines to site edges
            if (startSite != null) p1 = GetIntersection(startSite.Bounds, p2, p1);
            if (endSite != null) p2 = GetIntersection(endSite.Bounds, p1, p2);

            DrawLine(spriteBatch, p1, p2, Color.DarkGray, 2);
        }
    }
}