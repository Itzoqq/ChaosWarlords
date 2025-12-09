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
        private bool _wasClicking = false;
        public Texture2D PixelTexture { get; set; }

        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            _nodes = nodes;
            Sites = sites;
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

        public void Update(MouseState mouseState, Player currentPlayer)
        {
            foreach (var node in _nodes) node.Update(mouseState);

            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;

            if (isClicking && !_wasClicking)
            {
                HandleClick(currentPlayer);
            }

            _wasClicking = isClicking;
        }

        private void HandleClick(Player currentPlayer)
        {
            foreach (var node in _nodes)
            {
                if (node.IsHovered)
                {
                    if (CanDeployAt(node, currentPlayer.Color))
                    {
                        if (currentPlayer.Power >= 1 && currentPlayer.TroopsInBarracks > 0)
                        {
                            currentPlayer.Power -= 1;
                            currentPlayer.TroopsInBarracks--;

                            node.Occupant = currentPlayer.Color;
                            GameLogger.Log($"Deployed Troop at Node {node.Id}. Supply: {currentPlayer.TroopsInBarracks}", LogChannel.Combat);

                            UpdateSiteControl(currentPlayer);

                            if (currentPlayer.TroopsInBarracks == 0)
                                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
                        }
                        else if (currentPlayer.TroopsInBarracks == 0)
                        {
                            GameLogger.Log("Cannot Deploy: Barracks Empty!", LogChannel.Error);
                        }
                        else
                        {
                            GameLogger.Log("Cannot Deploy: Not enough Power!", LogChannel.Economy);
                        }
                    }
                    else
                    {
                        GameLogger.Log($"Invalid Deployment at Node {node.Id}: No Presence!", LogChannel.Error);
                    }
                    return;
                }
            }
        }

        private void UpdateSiteControl(Player activePlayer)
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

                bool newTotalControl = (newOwner == PlayerColor.Red && redCount == totalSpots) ||
                                       (newOwner == PlayerColor.Blue && blueCount == totalSpots);

                // Check Control Change
                if (newOwner != previousOwner)
                {
                    site.Owner = newOwner;
                    if (newOwner == activePlayer.Color)
                    {
                        // --- FIX: Only CITIES give immediate rewards ---
                        if (site.IsCity)
                        {
                            ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                            GameLogger.Log($"Seized Control of {site.Name}! +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);
                        }
                        else
                        {
                            GameLogger.Log($"Seized Control of {site.Name} (Scoring at Game End)", LogChannel.Combat);
                        }
                    }
                }

                // Check Total Control Change
                if (newTotalControl != previousTotal)
                {
                    site.HasTotalControl = newTotalControl;
                    if (newTotalControl && newOwner == activePlayer.Color)
                    {
                        // --- FIX: Only CITIES give immediate bonuses ---
                        if (site.IsCity)
                        {
                            ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                            GameLogger.Log($"Total Control established in {site.Name}! +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
                        }
                        else
                        {
                            GameLogger.Log($"Total Control established in {site.Name} (Scoring at Game End)", LogChannel.Combat);
                        }
                    }
                }
            }
        }

        private void ApplyReward(Player player, ResourceType type, int amount)
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

        public void ReturnTroop(MapNode node, Player ownerOfTroop)
        {
            if (node.Occupant != ownerOfTroop.Color) return;

            node.Occupant = PlayerColor.None;
            ownerOfTroop.TroopsInBarracks++;
            GameLogger.Log($"Returned troop at Node {node.Id} to barracks.", LogChannel.Combat);

            UpdateSiteControl(ownerOfTroop);
        }

        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            if (targetNode.Occupant != PlayerColor.None) return false;

            bool hasAnyTroops = false;
            foreach (var n in _nodes)
            {
                if (n.Occupant == player)
                {
                    hasAnyTroops = true;
                    break;
                }
            }

            if (!hasAnyTroops) return true;

            foreach (var neighbor in targetNode.Neighbors)
            {
                if (neighbor.Occupant == player) return true;
            }

            return false;
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
            if (Sites == null) return null;
            foreach (var site in Sites)
            {
                if (site.Nodes.Contains(node)) return site;
            }
            return null;
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