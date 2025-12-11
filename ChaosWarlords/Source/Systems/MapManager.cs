using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
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
            _nodeSiteLookup = new Dictionary<MapNode, Site>();

            if (sites != null)
            {
                foreach (var site in sites)
                    foreach (var node in site.Nodes)
                        _nodeSiteLookup[node] = site;
            }
        }

        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (_nodes.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in _nodes)
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            float mapCenterX = (minX + maxX) / 2f;
            float mapCenterY = (minY + maxY) / 2f;
            float screenCenterX = screenWidth / 2f;
            float screenCenterY = screenHeight / 2f;
            Vector2 offset = new Vector2(screenCenterX - mapCenterX, screenCenterY - mapCenterY);

            foreach (var node in _nodes)
            {
                node.Position += offset;
            }

            if (Sites != null)
            {
                foreach (var site in Sites)
                {
                    site.RecalculateBounds();
                }
            }
        }

        public void Update(MouseState mouseState)
        {
            // Only update visual states (hovering), do NOT handle clicks here.
            foreach (var node in _nodes) node.Update(mouseState);
        }

        public bool TryDeploy(Player currentPlayer)
        {
            var hoveredNode = GetHoveredNode();
            if (hoveredNode == null)
            {
                return false; // Clicked on nothing.
            }

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

            // All checks passed, execute deployment
            currentPlayer.Power -= 1;
            currentPlayer.TroopsInBarracks--;
            hoveredNode.Occupant = currentPlayer.Color;
            GameLogger.Log($"Deployed Troop at Node {hoveredNode.Id}. Supply: {currentPlayer.TroopsInBarracks}", LogChannel.Combat);
            UpdateSiteControl(currentPlayer);
            if (currentPlayer.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
            return true; // Action successful
        }

        public void UpdateSiteControl(Player activePlayer)
        {
            if (Sites == null) return;

            foreach (var site in Sites)
            {
                PlayerColor previousOwner = site.Owner;
                bool previousTotal = site.HasTotalControl;

                int redCount = 0;
                int blueCount = 0;
                int neutralCount = 0;
                int totalSpots = site.Nodes.Count;

                foreach (var node in site.Nodes)
                {
                    if (node.Occupant == PlayerColor.Red) redCount++;
                    if (node.Occupant == PlayerColor.Blue) blueCount++;
                    if (node.Occupant == PlayerColor.Neutral) neutralCount++;
                }

                PlayerColor newOwner = PlayerColor.None;
                if (redCount > blueCount && redCount > neutralCount) newOwner = PlayerColor.Red;
                else if (blueCount > redCount && blueCount > neutralCount) newOwner = PlayerColor.Blue;

                bool ownsAllNodes = (newOwner == PlayerColor.Red && redCount == totalSpots) ||
                                    (newOwner == PlayerColor.Blue && blueCount == totalSpots);

                bool hasEnemySpy = false;
                if (newOwner != PlayerColor.None)
                {
                    foreach (var spy in site.Spies)
                    {
                        if (spy != newOwner) hasEnemySpy = true;
                    }
                }

                bool newTotalControl = ownsAllNodes && !hasEnemySpy;

                // Check Control Change
                if (newOwner != previousOwner)
                {
                    site.Owner = newOwner;
                    if (newOwner == activePlayer.Color && site.IsCity)
                    {
                        ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                        GameLogger.Log($"Seized Control of {site.Name}!", LogChannel.Economy);
                    }
                }

                if (newTotalControl != previousTotal)
                {
                    site.HasTotalControl = newTotalControl;
                    if (newTotalControl && newOwner == activePlayer.Color && site.IsCity)
                    {
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                        GameLogger.Log($"Total Control established in {site.Name}!", LogChannel.Economy);
                    }
                    else if (!newTotalControl && previousTotal && previousOwner == activePlayer.Color)
                    {
                        GameLogger.Log($"Lost Total Control of {site.Name} (Spies or Troops lost).", LogChannel.Combat);
                    }
                }
            }
        }

        public void ApplyReward(Player player, ResourceType type, int amount)
        {
            if (type == ResourceType.Power) player.Power += amount;
            if (type == ResourceType.Influence) player.Influence += amount;
            if (type == ResourceType.VictoryPoints) player.VictoryPoints += amount;
        }

        public void Assassinate(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None) return;
            if (node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
            GameLogger.Log($"Assassinated enemy at Node {node.Id}. Trophy Hall: {attacker.TrophyHall}", LogChannel.Combat);

            UpdateSiteControl(attacker);
        }

        public void ReturnTroop(MapNode node, Player requestingPlayer)
        {
            // Safety Check (Should be caught by Game1, but good to have)
            if (node.Occupant == PlayerColor.Neutral) return;

            // If it is my troop
            if (node.Occupant == requestingPlayer.Color)
            {
                node.Occupant = PlayerColor.None;
                requestingPlayer.TroopsInBarracks++;
                GameLogger.Log($"Returned friendly troop at Node {node.Id} to barracks.", LogChannel.Combat);
            }
            // If it is an enemy troop (Not Neutral, Not None)
            else if (node.Occupant != PlayerColor.None)
            {
                PlayerColor enemyColor = node.Occupant;
                node.Occupant = PlayerColor.None;
                // In a full game, we would find Player(enemyColor) and ++ their barracks
                GameLogger.Log($"Returned {enemyColor} troop at Node {node.Id} to their barracks.", LogChannel.Combat);
            }

            UpdateSiteControl(requestingPlayer);
        }

        public void PlaceSpy(Site site, Player player)
        {
            // Rule: Can only place 1 spy per site per player
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

                // Placing a spy might break someone else's Total Control
                UpdateSiteControl(player);
            }
            else
            {
                GameLogger.Log("No Spies left in supply!", LogChannel.Error);
            }
        }

        public bool ReturnSpy(Site site, Player activePlayer)
        {
            // 1. Check for Presence at the site
            // (We can check presence on any node within the site, as presence is site-wide)
            if (site.Nodes.Count > 0 && !HasPresence(site.Nodes[0], activePlayer.Color))
            {
                GameLogger.Log("Cannot return spy: No Presence at this Site!", LogChannel.Error);
                return false;
            }

            // 2. Find an enemy spy
            PlayerColor spyToRemove = PlayerColor.None;
            foreach (var spyColor in site.Spies)
            {
                if (spyColor != activePlayer.Color && spyColor != PlayerColor.None)
                {
                    spyToRemove = spyColor;
                    break; // Just remove the first enemy found for now
                }
            }

            if (spyToRemove == PlayerColor.None)
            {
                GameLogger.Log("Invalid Target: No enemy spies at this Site.", LogChannel.Error);
                return false;
            }

            // 3. Execute Removal
            site.Spies.Remove(spyToRemove);

            // In a full multiplayer game, we would find the Player object for 'spyToRemove' 
            // and increment their SpiesInBarracks. For 2-player local, we assume it's the opponent.
            GameLogger.Log($"Returned {spyToRemove} Spy from {site.Name} to barracks.", LogChannel.Combat);

            // 4. Recalculate Control (Removing a spy might grant Total Control)
            UpdateSiteControl(activePlayer);

            return true;
        }

        public void Supplant(MapNode node, Player attacker)
        {
            // 1. Assassinate Logic
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
            GameLogger.Log($"Supplanted enemy at Node {node.Id} (Added to Trophy Hall)", LogChannel.Combat);

            // 2. Deploy Logic (Free, no power cost, supply checked in Game1)
            node.Occupant = attacker.Color;
            attacker.TroopsInBarracks--;

            UpdateSiteControl(attacker);
        }

        public bool CanAssassinate(MapNode target, Player attacker)
        {
            // Must be occupied
            if (target.Occupant == PlayerColor.None) return false;

            // Cannot assassinate yourself
            if (target.Occupant == attacker.Color) return false;

            // Must have "Presence" (Site-aware adjacency)
            return HasPresence(target, attacker.Color);
        }

        // 2. UPDATE/REPLACE THIS METHOD
        public bool HasPresence(MapNode targetNode, PlayerColor player)
        {
            // 1. CHECK SPIES (Presence via Subterfuge)
            // If the target node is in a Site, and we have a spy there, we have presence.
            Site parentSite = GetSiteForNode(targetNode);
            if (parentSite != null)
            {
                if (parentSite.Spies.Contains(player)) return true;
            }

            // 2. CHECK ADJACENCY (Presence via Troops)
            // Determine Scope: Are we targeting a specific Node, or the whole Site?
            List<MapNode> nodesToCheck = new List<MapNode>();

            if (parentSite != null)
            {
                // If targeting a Site node, we check neighbors of the WHOLE Site
                nodesToCheck.AddRange(parentSite.Nodes);
            }
            else
            {
                // Otherwise, just check this single node
                nodesToCheck.Add(targetNode);
            }

            // Check if any neighbor of the target area contains our troop
            foreach (var node in nodesToCheck)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    if (neighbor.Occupant == player) return true;
                }
            }

            return false;
        }

        // 3. (Optional but Recommended) UPDATE THIS METHOD TO USE THE FIX
        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            // 1. Occupied check
            if (targetNode.Occupant != PlayerColor.None) return false;

            // 2. "Start of Game" Rule
            // If we have ZERO troops on the board, we can deploy anywhere (currently, until we implement starting sites (black sites in the Tyrants..)).
            bool hasAnyTroops = false;
            foreach (var n in _nodes)
            {
                if (n.Occupant == player)
                {
                    hasAnyTroops = true;
                    break;
                }
            }

            if (!hasAnyTroops) return true; // Allow initial deployment

            // 3. Standard Rule: Must have Presence
            return HasPresence(targetNode, player);
        }

        public MapNode GetHoveredNode()
        {
            foreach (var node in _nodes)
            {
                if (node.IsHovered) return node;
            }
            return null;
        }

        public Site GetHoveredSite(Vector2 mousePos)
        {
            if (Sites == null) return null;
            foreach (var site in Sites)
            {
                if (site.Bounds.Contains(mousePos)) return site;
            }
            return null;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (Sites != null)
            {
                foreach (var site in Sites)
                {
                    site.Draw(spriteBatch, font, PixelTexture);
                }
            }

            foreach (var node in _nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    if (node.Id < neighbor.Id)
                    {
                        Site startSite = GetSiteForNode(node);
                        Site endSite = GetSiteForNode(neighbor);

                        if (startSite != null && startSite == endSite) continue;

                        Vector2 p1 = startSite != null ? startSite.Bounds.Center.ToVector2() : node.Position;
                        Vector2 p2 = endSite != null ? endSite.Bounds.Center.ToVector2() : neighbor.Position;

                        if (startSite != null) p1 = GetIntersection(startSite.Bounds, p2, p1);
                        if (endSite != null) p2 = GetIntersection(endSite.Bounds, p1, p2);

                        DrawLine(spriteBatch, p1, p2, Color.DarkGray, 2);
                    }
                }
            }

            foreach (var node in _nodes)
            {
                node.Draw(spriteBatch);
            }
        }

        private Site GetSiteForNode(MapNode node)
        {
            _nodeSiteLookup.TryGetValue(node, out Site site);
            return site;
        }

        private Vector2 GetIntersection(Rectangle rect, Vector2 start, Vector2 end)
        {
            Vector2[] corners = new Vector2[]
            {
                new Vector2(rect.Left, rect.Top),
                new Vector2(rect.Right, rect.Top),
                new Vector2(rect.Right, rect.Bottom),
                new Vector2(rect.Left, rect.Bottom)
            };

            float closestDist = float.MaxValue;
            Vector2 closestPoint = end;

            for (int i = 0; i < 4; i++)
            {
                Vector2 p1 = corners[i];
                Vector2 p2 = corners[(i + 1) % 4];

                if (TryGetLineIntersection(start, end, p1, p2, out Vector2 intersection))
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

        private bool TryGetLineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 result)
        {
            result = Vector2.Zero;
            float d = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
            if (d == 0) return false;

            float ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / d;
            float ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / d;

            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                result = new Vector2(p1.X + ua * (p2.X - p1.X), p1.Y + ua * (p2.Y - p1.Y));
                return true;
            }
            return false;
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
    }
}