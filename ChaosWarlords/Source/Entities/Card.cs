using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Card
    {
        // --- Data ---
        // Changed to 'internal set' so Factory/Tests can set them, but UI cannot.
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public int Cost { get; internal set; }
        public CardAspect Aspect { get; internal set; }

        // Victory Points
        public int DeckVP { get; internal set; }
        public int InnerCircleVP { get; internal set; }

        // Logic
        public List<CardEffect> Effects { get; internal set; } = new List<CardEffect>();

        // Location is managed by Game Logic (internal), not UI
        public CardLocation Location { get; internal set; }

        // --- Rendering ---
        private Texture2D _texture;

        // Position stays 'public set' because animations/UI need to move cards freely
        public Vector2 Position { get; set; }

        // IsHovered is calculated by logic, UI just reads it
        public bool IsHovered { get; private set; }

        // Constants for drawing
        public const int Width = 150;
        public const int Height = 200;

        public Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerVp)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Aspect = aspect;
            DeckVP = deckVp;
            InnerCircleVP = innerVp;
            Location = CardLocation.Deck;
        }

        public void SetTexture(Texture2D texture)
        {
            _texture = texture;
        }

        public void AddEffect(CardEffect effect)
        {
            Effects.Add(effect);
        }

        public void Update(GameTime gameTime, MouseState mouseState)
        {
            // Simple AABB collision for hover detection
            Rectangle bounds = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            IsHovered = bounds.Contains(mouseState.Position);
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            // 1. Draw Background (Texture or Placeholder)
            if (_texture != null)
            {
                Color tint = GetTintColor();
                spriteBatch.Draw(_texture, new Rectangle((int)Position.X, (int)Position.Y, Width, Height), tint);
            }

            // 2. Draw Text Content
            DrawTextContent(spriteBatch, font);
        }

        private Color GetTintColor()
        {
            Color color = Color.White;

            // Highlight if hovered
            if (IsHovered)
            {
                color = Color.Lerp(color, Color.Yellow, 0.3f);
            }

            return color;
        }

        private void DrawTextContent(SpriteBatch spriteBatch, SpriteFont font)
        {
            // A. Name (Top)
            spriteBatch.DrawString(font, Name, Position + new Vector2(5, 5), Color.Black);

            // B. Effect Description (Middle)
            string effectText = BuildEffectText();
            // Using a slightly different color or offset for effect text to make it pop
            spriteBatch.DrawString(font, effectText, Position + new Vector2(10, 50), Color.DarkRed);

            // C. Cost (Bottom)
            spriteBatch.DrawString(font, $"Cost: {Cost}", Position + new Vector2(10, Height - 25), Color.Blue);
        }

        // Kept 'internal' so tests can verify the text generation if needed
        internal string BuildEffectText()
        {
            // If the card has a static description (from JSON), prefer that.
            if (!string.IsNullOrEmpty(Description))
                return Description;

            // Otherwise, generate text dynamically from effects
            string text = "";
            foreach (var effect in Effects)
            {
                if (effect.Type == EffectType.GainResource)
                {
                    text += $"+{effect.Amount} {effect.TargetResource}\n";
                }
                else
                {
                    // Fallback for other effects
                    text += $"{effect.Type}\n";
                }
            }
            return text;
        }
    }
}