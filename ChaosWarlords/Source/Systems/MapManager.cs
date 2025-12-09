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

        // FIX: Changed from 'private List<Site> _sites' to a Public Property
        public List<Site> Sites { get; private set; }

        private bool _wasClicking = false;

        // FIX: Updated constructor to assign to the new Property
        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            _nodes = nodes;
            Sites = sites;
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
                        if (currentPlayer.Power >= 1)
                        {
                            currentPlayer.Power -= 1;
                            node.Occupant = currentPlayer.Color;
                            GameLogger.Log($"Deployed Troop at Node {node.Id}. Remaining Power: {currentPlayer.Power}", LogChannel.Combat);

                            // TRIGGER UPDATE with the player who made the move
                            UpdateSiteControl(currentPlayer);
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
                // 1. Snapshot CURRENT state before recalculating
                PlayerColor previousOwner = site.Owner;
                bool previousTotal = site.HasTotalControl;

                // 2. Count Troops
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

                // 3. Determine NEW State
                PlayerColor newOwner = PlayerColor.None;
                if (redCount > blueCount && redCount > neutralCount) newOwner = PlayerColor.Red;
                else if (blueCount > redCount && blueCount > neutralCount) newOwner = PlayerColor.Blue;

                bool newTotalControl = (newOwner == PlayerColor.Red && redCount == totalSpots) ||
                                       (newOwner == PlayerColor.Blue && blueCount == totalSpots);

                // 4. APPLY CHANGES & IMMEDIATE REWARDS

                // Case A: Control Gained (Majority)
                if (newOwner != previousOwner)
                {
                    site.Owner = newOwner; // Update state

                    if (newOwner == activePlayer.Color)
                    {
                        // IMMEDIATE REWARD!
                        ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                        GameLogger.Log($"Seized Control of {site.Name}! +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);
                    }
                    else if (newOwner != PlayerColor.None)
                    {
                        // Someone else took it
                        GameLogger.Log($"{site.Name} was taken by {newOwner}!", LogChannel.Combat);
                    }
                }

                // Case B: Total Control Achieved
                if (newTotalControl != previousTotal)
                {
                    site.HasTotalControl = newTotalControl; // Update state

                    if (newTotalControl && newOwner == activePlayer.Color)
                    {
                        // IMMEDIATE BONUS!
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                        GameLogger.Log($"Total Control established in {site.Name}! +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
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

        // PRESENCE LOGIC
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
            foreach (var node in _nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    DrawLine(spriteBatch, node.Position, neighbor.Position, Color.DarkGray, 2);
                }
            }

            foreach (var node in _nodes)
            {
                node.Draw(spriteBatch);
            }

            if (Sites != null)
            {
                foreach (var site in Sites)
                {
                    site.Draw(spriteBatch, font);
                }
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
        {
            if (PixelTexture == null) return;

            Vector2 edge = end - start;
            float angle = (float)System.Math.Atan2(edge.Y, edge.X);

            spriteBatch.Draw(PixelTexture,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null,
                color,
                angle,
                new Vector2(0, 0.5f),
                SpriteEffects.None,
                0);
        }

        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (_nodes.Count == 0) return;

            // 1. Find the bounds of the current map
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in _nodes)
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            // 2. Calculate the center of the map bounds
            float mapCenterX = (minX + maxX) / 2f;
            float mapCenterY = (minY + maxY) / 2f;

            // 3. Calculate the center of the screen
            float screenCenterX = screenWidth / 2f;
            float screenCenterY = screenHeight / 2f;

            // 4. Determine the offset needed
            Vector2 offset = new Vector2(screenCenterX - mapCenterX, screenCenterY - mapCenterY);

            // 5. Apply offset to ALL nodes
            foreach (var node in _nodes)
            {
                node.Position += offset;
            }

            // 6. Recalculate Site centers (since nodes moved)
            if (Sites != null)
            {
                foreach (var site in Sites)
                {
                    // We need to re-run the internal centering logic. 
                    // Since that logic is private in Site.cs, we can either make it public or just 
                    // manually update the label position by the same offset.
                    site.LabelPosition += offset;
                }
            }
        }

        public Texture2D PixelTexture { get; set; }
    }
}