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

        // Split VP into Deck and Inner Circle values
        public int DeckVP { get; private set; }
        public int InnerCircleVP { get; private set; }

        public int InfluenceValue { get; private set; } // For "Influence" aspect cards
        public List<CardEffect> Effects { get; private set; } = new List<CardEffect>();

        public string Description { get; set; } = "";
        public CardLocation Location { get; set; }

        // UI State
        public Vector2 Position { get; set; }
        public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
        public bool IsHovered { get; set; }

        // Constants
        public const int Width = 150;
        public const int Height = 200;

        // --- UPDATED CONSTRUCTOR ---
        public Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerCircleVp, int influence)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Aspect = aspect;
            DeckVP = deckVp;
            InnerCircleVP = innerCircleVp;
            InfluenceValue = influence;
        }

        public void AddEffect(CardEffect effect)
        {
            Effects.Add(effect);
        }

        public Card Clone()
        {
            // --- UPDATED CLONE ---
            var newCard = new Card(Id, Name, Cost, Aspect, DeckVP, InnerCircleVP, InfluenceValue)
            {
                Description = Description,
                Location = Location,
                Position = Position,
                IsHovered = IsHovered
            };

            foreach (var effect in Effects)
            {
                newCard.Effects.Add(new CardEffect(effect.Type, effect.Amount, effect.TargetResource));
            }

            return newCard;
        }
    }
}