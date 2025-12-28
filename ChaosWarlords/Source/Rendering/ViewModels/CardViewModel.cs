using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Cards;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.ViewModels
{
    [ExcludeFromCodeCoverage]
    public class CardViewModel
    {
        // Reference to the pure Logic Data
        public Card Model { get; }

        // UI State
        public Vector2 Position { get; set; }
        public bool IsHovered { get; set; }

        // Calculated Bounds based on Position + Constants
        // Note: Card.Width/Height constants can stay in Card.cs if they represent
        // "Standard Card Size", or move to a config/layout class.
        public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, Card.Width, Card.Height);

        public CardViewModel(Card model)
        {
            Model = model;
            Position = Vector2.Zero;
            IsHovered = false;
        }

        // Helper to keep code clean in GameplayState
        public bool Contains(Point point)
        {
            return Bounds.Contains(point);
        }
    }
}


