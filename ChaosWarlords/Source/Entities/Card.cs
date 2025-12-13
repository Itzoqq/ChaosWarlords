using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Card
    {
        // Core Data
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Cost { get; private set; }
        public CardAspect Aspect { get; private set; }
        public int VictoryPoints { get; private set; }
        public int InfluenceValue { get; private set; } // For "Influence" aspect cards
        public List<CardEffect> Effects { get; private set; } = new List<CardEffect>();

        public string Description { get; set; } = "";
        public CardLocation Location { get; set; }

        // UI State (Kept here for simplicity in this refactor, though ideally belongs in a View-Model)
        public Vector2 Position { get; set; }
        public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
        public bool IsHovered { get; set; } // Logic can still know "this card is selected"

        // Constants
        public const int Width = 150;
        public const int Height = 200;

        public Card(string id, string name, int cost, CardAspect aspect, int vp, int influence)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Aspect = aspect;
            VictoryPoints = vp;
            InfluenceValue = influence;
        }

        public void AddEffect(CardEffect effect)
        {
            Effects.Add(effect);
        }
    }
}