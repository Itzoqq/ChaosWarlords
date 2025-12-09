using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Site
    {
        public string Name { get; private set; }

        // --- NEW: Flexible Rewards ---
        public ResourceType ControlResource { get; private set; }
        public int ControlAmount { get; private set; }
        public bool IsCity { get; set; }

        public ResourceType TotalControlResource { get; private set; }
        public int TotalControlAmount { get; private set; }

        public List<MapNode> Nodes { get; private set; } = new List<MapNode>();
        public PlayerColor Owner { get; set; } = PlayerColor.None;
        public bool HasTotalControl { get; set; } = false;

        public Vector2 LabelPosition { get; set; }

        // Updated Constructor
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
            RecalculateCenter();
        }

        private void RecalculateCenter()
        {
            if (Nodes.Count == 0) return;
            Vector2 sum = Vector2.Zero;
            foreach (var node in Nodes) sum += node.Position;
            LabelPosition = sum / Nodes.Count;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixelTexture)
        {
            Color primaryColor = (Owner == PlayerColor.None) ? Color.Gray : GetColor(Owner);
            Vector2 drawPos = LabelPosition;

            // --- VISUAL: CITY CONTROL MARKER ---
            // If it is a City, we draw a "Big Circle" (Control Marker slot)
            if (IsCity)
            {
                int radius = 35; // Bigger than troop nodes (20)

                // Draw filled background (Black if empty, Player Color if owned)
                Color fillColor = (Owner == PlayerColor.None) ? Color.Black * 0.5f : primaryColor;

                // Draw Circle (Simple approximation using the pixel texture scaled)
                // In a real game, you would load a specific "SiteCircle.png"
                Rectangle circleRect = new Rectangle(
                    (int)drawPos.X - radius,
                    (int)drawPos.Y - radius,
                    radius * 2,
                    radius * 2);

                spriteBatch.Draw(pixelTexture, circleRect, fillColor);

                // Shift text down so it doesn't overlap the marker
                drawPos.Y += 45;
            }

            // --- TEXT LABEL ---
            string text = Name.ToUpper();

            // Append Resource Info
            if (Owner != PlayerColor.None)
            {
                string reward = HasTotalControl
                    ? $"+{TotalControlAmount} {TotalControlResource} (MAX)"
                    : $"+{ControlAmount} {ControlResource}";
                text += $"\n{reward}";
            }
            else
            {
                // Show potential
                text += $"\n({ControlAmount} {ControlResource})";
            }

            Vector2 textSize = font.MeasureString(text);

            // Center the text
            Vector2 textOrigin = new Vector2(textSize.X / 2, textSize.Y / 2);

            // Draw Shadow
            spriteBatch.DrawString(font, text, drawPos + new Vector2(1, 1), Color.Black, 0f, textOrigin, 1f, SpriteEffects.None, 0f);
            // Draw Text
            spriteBatch.DrawString(font, text, drawPos, IsCity ? Color.Gold : Color.LightGray, 0f, textOrigin, 1f, SpriteEffects.None, 0f);
        }

        private Color GetColor(PlayerColor p)
        {
            if (p == PlayerColor.Red) return Color.Red;
            if (p == PlayerColor.Blue) return Color.Blue;
            return Color.White;
        }
    }
}