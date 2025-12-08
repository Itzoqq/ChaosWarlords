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
            // Background Color based on Aspect (visual debugging)
            Color bgColor = Aspect switch
            {
                CardAspect.Warlord => Color.Crimson,
                CardAspect.Sorcery => Color.Purple,
                CardAspect.Shadow => Color.Black,
                _ => Color.Gray
            };

            if (IsHovered) bgColor = Color.Lerp(bgColor, Color.White, 0.3f);

            // Draw Card Body
            if (_texture != null)
                spriteBatch.Draw(_texture, _bounds, bgColor);

            // Draw Text (If we have a font loaded, otherwise skip for now)
            if (font != null)
            {
                spriteBatch.DrawString(font, Name, Position + new Vector2(10, 10), Color.White);
                spriteBatch.DrawString(font, $"{Cost} Inf", Position + new Vector2(10, Height - 25), Color.Gold);
            }
        }
    }
}