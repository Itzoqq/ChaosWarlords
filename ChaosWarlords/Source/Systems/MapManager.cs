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
        
        // This tracks which mouse button state we are in to prevent "rapid fire" clicks
        private bool _wasClicking = false;

        public MapManager(List<MapNode> nodes)
        {
            _nodes = nodes;
        }

        public void Update(MouseState mouseState, Player currentPlayer)
        {
            foreach (var node in _nodes) node.Update(mouseState);

            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;

            if (isClicking && !_wasClicking)
            {
                HandleClick(currentPlayer); // Pass the player
            }

            _wasClicking = isClicking;
        }

        // UPDATE 1: Change the signature to accept the full Player object
        private void HandleClick(Player currentPlayer)
        {
            foreach (var node in _nodes)
            {
                if (node.IsHovered)
                {
                    // RULE CHECK 1: Can we deploy based on position?
                    if (CanDeployAt(node, currentPlayer.Color))
                    {
                        // RULE CHECK 2: Can we afford it? (Cost is 1 Power)
                        if (currentPlayer.Power >= 1)
                        {
                            // SUCCESS: Spend resource and place unit
                            currentPlayer.Power -= 1;
                            node.Occupant = currentPlayer.Color;
                            System.Diagnostics.Debug.WriteLine($"Deployed! Remaining Power: {currentPlayer.Power}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Not enough Power!");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid Deployment: No Presence!");
                    }
                    return; 
                }
            }
        }

        // --- THE GOLDEN RULE: PRESENCE ---
        public bool CanDeployAt(MapNode targetNode, PlayerColor player)
        {
            // Rule 1: The spot must be empty
            if (targetNode.Occupant != PlayerColor.None)
                return false;

            // Rule 2: If we have NO troops on the entire board, we can deploy anywhere (Start of game rule)
            // We check if the player exists anywhere on the map
            bool hasAnyTroops = false;
            foreach(var n in _nodes)
            {
                if (n.Occupant == player) 
                {
                    hasAnyTroops = true;
                    break;
                }
            }

            if (!hasAnyTroops) return true; // First deployment is free

            // Rule 3: Must have "Presence" (an adjacent friendly unit)
            // (Later we will add Spies and Site control to this check)
            foreach (var neighbor in targetNode.Neighbors)
            {
                if (neighbor.Occupant == player)
                {
                    return true; // We found a buddy next door!
                }
            }

            return false;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw connections first
            foreach (var node in _nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    DrawLine(spriteBatch, node.Position, neighbor.Position, Color.DarkGray, 2);
                }
            }

            // Draw nodes on top
            foreach (var node in _nodes)
            {
                node.Draw(spriteBatch);
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
        
        // Actually, to make DrawLine work easily without passing textures around constantly:
        // Let's add a public Texture2D PixelTexture property to this class.
        public Texture2D PixelTexture { get; set; }
    }
}