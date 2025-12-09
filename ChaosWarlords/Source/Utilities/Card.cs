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
        public int DeckVP { get; private set; }      // Points if in deck at end
        public int InnerCircleVP { get; private set; } // Points if promoted

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

        public void Draw(SpriteBatch spriteBatch, SpriteFont font = null)
        {
            // 1. Dynamic Background Color
            Color bgColor = Color.Gray;
            // Check effects to determine color
            foreach (var effect in Effects)
            {
                if (effect.TargetResource == ResourceType.Power) bgColor = Color.Firebrick; // Red for Power
                if (effect.TargetResource == ResourceType.Influence) bgColor = Color.CornflowerBlue; // Blue for Influence
            }

            // Hover Highlight
            if (IsHovered) bgColor = Color.Lerp(bgColor, Color.White, 0.3f);

            // 2. Draw Card Body
            if (_texture != null)
                spriteBatch.Draw(_texture, _bounds, bgColor);

            // 3. Draw Text Information
            if (font != null)
            {
                // A. Draw Name at top
                spriteBatch.DrawString(font, Name, Position + new Vector2(5, 5), Color.White);

                // B. Draw Effect Text in the middle
                string effectText = "";
                foreach (var effect in Effects)
                {
                    if (effect.Type == EffectType.GainResource)
                    {
                        if (effect.TargetResource == ResourceType.Power) effectText += $"+{effect.Amount} Power\n";
                        if (effect.TargetResource == ResourceType.Influence) effectText += $"+{effect.Amount} Influence\n";
                    }
                }

                // Draw the effect text centered-ish
                spriteBatch.DrawString(font, effectText, Position + new Vector2(10, 50), Color.Yellow);

                // C. Draw Cost at bottom
                spriteBatch.DrawString(font, $"Cost: {Cost}", Position + new Vector2(10, Height - 25), Color.LightGray);
            }
        }
    }
}