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

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            Color nameColor = (Owner == PlayerColor.None) ? Color.LightGray : GetColor(Owner);
            
            string text = Name;
            
            if (Owner != PlayerColor.None)
            {
                // Show Base Control
                text += $"\n[Control: +{ControlAmount} {ControlResource}]";

                // Show Bonus if active
                if (HasTotalControl)
                {
                    text += $"\n[TOTAL BONUS: +{TotalControlAmount} {TotalControlResource}]";
                }
            }
            else
            {
                // Neutral View
                text += $"\n({ControlAmount} {ControlResource})";
            }

            Vector2 size = font.MeasureString(text);
            spriteBatch.DrawString(font, text, LabelPosition - (size / 2) + new Vector2(0, -60), nameColor);
        }

        private Color GetColor(PlayerColor p) 
        {
            if (p == PlayerColor.Red) return Color.Red;
            if (p == PlayerColor.Blue) return Color.Blue;
            return Color.White;
        }
    }
}