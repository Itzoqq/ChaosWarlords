using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Site
    {
        public string Name { get; private set; }

        public ResourceType ControlResource { get; private set; }
        public int ControlAmount { get; private set; }

        public ResourceType TotalControlResource { get; private set; }
        public int TotalControlAmount { get; private set; }

        public bool IsCity { get; set; }

        public List<MapNode> Nodes { get; private set; } = new List<MapNode>();
        public PlayerColor Owner { get; set; } = PlayerColor.None;
        public HashSet<PlayerColor> Spies { get; private set; } = new HashSet<PlayerColor>();
        public bool HasTotalControl { get; set; } = false;

        // Visual Bounds
        public Rectangle Bounds { get; private set; }

        public Site(string name,
                    ResourceType controlType, int controlAmt,
                    ResourceType totalType, int totalAmt)
        {
            Name = name;
            ControlResource = controlType;
            ControlAmount = controlAmt;
            TotalControlResource = totalType;
            TotalControlAmount = totalAmt;
        }

        public void AddNode(MapNode node)
        {
            Nodes.Add(node);
            RecalculateBounds();
        }

        // --- FIX: CHANGED TO PUBLIC ---
        internal void RecalculateBounds()
        {
            if (Nodes.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in Nodes)
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            // --- FIX: Asymmetrical Padding ---
            int sidePadding = 35;
            int topPadding = 70; // Extra space at top for the Name!
            int bottomPadding = 35;

            int width = (int)(maxX - minX) + (sidePadding * 2);
            int height = (int)(maxY - minY) + topPadding + bottomPadding;

            // Shift Y up by 'topPadding' to create the header space
            Bounds = new Rectangle((int)minX - sidePadding, (int)minY - topPadding, width, height);
        }

        public int GetTroopCount(PlayerColor color)
        {
            return Nodes.Count(n => n.Occupant == color);
        }

        public PlayerColor GetControllingPlayer()
        {
            var troopCounts = Nodes
                .Where(n => n.Occupant != PlayerColor.None && n.Occupant != PlayerColor.Neutral)
                .GroupBy(n => n.Occupant)
                .Select(g => new { Player = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            if (troopCounts.Count == 0)
            {
                return PlayerColor.None; // No player troops on site
            }

            // Check for a tie for the highest count
            if (troopCounts.Count > 1 && troopCounts[0].Count == troopCounts[1].Count)
            {
                return PlayerColor.None;
            }

            return troopCounts[0].Player; // Clear winner
        }

        public bool HasSpy(PlayerColor color)
        {
            return Spies.Contains(color);
        }

        public void AddSpy(PlayerColor color)
        {
            Spies.Add(color);
        }

        public bool RemoveSpy(PlayerColor color)
        {
            return Spies.Remove(color);
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixelTexture)
        {
            // 1. Draw Background Rectangle
            spriteBatch.Draw(pixelTexture, Bounds, Color.Black * 0.5f);

            // 2. Draw Border (Gray if empty, Player Color if owned)
            Color borderColor = (Owner == PlayerColor.None) ? Color.Gray : GetColor(Owner);

            // Draw 4 lines for the border
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, 2), borderColor); // Top
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X, Bounds.Bottom, Bounds.Width, 2), borderColor); // Bottom
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X, Bounds.Y, 2, Bounds.Height), borderColor); // Left
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.Right, Bounds.Y, 2, Bounds.Height), borderColor); // Right

            // 3. Draw Text (Upper Left Corner)
            string text = Name.ToUpper();

            // --- FIX: VISUALIZE ADDITIVE REWARDS ---
            if (Owner != PlayerColor.None)
            {
                // A. Always show the Base Control Reward
                text += $"\n[Control: +{ControlAmount} {ControlResource}]";

                // B. If Total Control, APPEND the Bonus (Don't replace!)
                if (HasTotalControl)
                {
                    text += $"\n[TOTAL BONUS: +{TotalControlAmount} {TotalControlResource}]";
                }
            }
            else
            {
                // Neutral view
                text += $"\n({ControlAmount} {ControlResource})";
            }

            Vector2 textPos = new Vector2(Bounds.X + 10, Bounds.Y + 10);

            // Draw Shadow
            spriteBatch.DrawString(font, text, textPos + new Vector2(1, 1), Color.Black);
            // Draw Text (Gold for Cities, LightGray for Sites)
            spriteBatch.DrawString(font, text, textPos, IsCity ? Color.Gold : Color.LightGray);

            // 4. Draw Spies (Top Left Corner, below text)
            int spySize = 12;
            int startX = Bounds.X - (spySize / 2);
            int startY = Bounds.Y - (spySize / 2);
            int i = 0;

            foreach (var spyColor in Spies)
            {
                Color drawColor = GetColor(spyColor);

                // Position: Top Left, moving slightly right for each additional spy
                Vector2 spyPos = new Vector2(startX + (i * (spySize + 2)), startY);

                // Draw Spy (Circle representation)
                // Since we use a 1x1 pixel, we draw a small square to represent the unit/spy for now
                Rectangle spyRect = new Rectangle((int)spyPos.X, (int)spyPos.Y, spySize, spySize);

                // Draw a black outline for visibility
                spriteBatch.Draw(pixelTexture, new Rectangle(spyRect.X - 1, spyRect.Y - 1, spyRect.Width + 2, spyRect.Height + 2), Color.Black);
                // Draw the spy color
                spriteBatch.Draw(pixelTexture, spyRect, drawColor);

                i++;
            }
        }

        private Color GetColor(PlayerColor p)
        {
            if (p == PlayerColor.Red) return Color.Red;
            if (p == PlayerColor.Blue) return Color.Blue;
            return Color.White;
        }
    }
}