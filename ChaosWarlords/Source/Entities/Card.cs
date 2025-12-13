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
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; set; }
        public int Cost { get; private set; }
        public CardAspect Aspect { get; private set; }

        // Victory Points
        public int DeckVP { get; private set; }
        public int InnerCircleVP { get; private set; }

        // Logic
        public List<CardEffect> Effects { get; private set; } = new List<CardEffect>();
        public CardLocation Location { get; set; }

        // --- Rendering ---
        private Texture2D _texture;
        private Rectangle _bounds;
        public Vector2 Position { get; set; }
        public bool IsHovered { get; set; }

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
            Location = CardLocation.Market;
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
            // Simple collision detection
            _bounds = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            IsHovered = _bounds.Contains(mouseState.Position);
        }

        // --- REFACTORED DRAW METHOD ---
        public void Draw(SpriteBatch spriteBatch, SpriteFont font = null)
        {
            // 1. Draw Background
            Color bgColor = DetermineBackgroundColor();
            if (_texture != null)
            {
                spriteBatch.Draw(_texture, _bounds, bgColor);
            }

            // 2. Draw Text
            if (font != null)
            {
                DrawTextContent(spriteBatch, font);
            }
        }

        private Color DetermineBackgroundColor()
        {
            Color color = Color.Gray;

            // Simple logic: Change color based on primary resource gain
            // (You could expand this to check Aspects instead for better theming)
            foreach (var effect in Effects)
            {
                if (effect.TargetResource == ResourceType.Power) color = Color.Firebrick;
                else if (effect.TargetResource == ResourceType.Influence) color = Color.CornflowerBlue;
            }

            if (IsHovered)
            {
                color = Color.Lerp(color, Color.White, 0.3f);
            }

            return color;
        }

        private void DrawTextContent(SpriteBatch spriteBatch, SpriteFont font)
        {
            // A. Name (Top)
            spriteBatch.DrawString(font, Name, Position + new Vector2(5, 5), Color.White);

            // B. Effect Description (Middle)
            string effectText = BuildEffectText();
            spriteBatch.DrawString(font, effectText, Position + new Vector2(10, 50), Color.Yellow);

            // C. Cost (Bottom)
            spriteBatch.DrawString(font, $"Cost: {Cost}", Position + new Vector2(10, Height - 25), Color.LightGray);
        }

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