using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Views
{
    [ExcludeFromCodeCoverage]
    public class CardRenderer
    {
        private Texture2D _pixelTexture;
        private SpriteFont _font;

        public CardRenderer(Texture2D pixelTexture, SpriteFont font)
        {
            _pixelTexture = pixelTexture;
            _font = font;
        }

        public void Draw(SpriteBatch sb, Card card)
        {
            // 1. Determine Color based on Aspect
            Color bgColor = GetAspectColor(card.Aspect);
            if (card.IsHovered) bgColor = Color.Lerp(bgColor, Color.White, 0.3f);

            // 2. Draw Background
            sb.Draw(_pixelTexture, card.Bounds, bgColor);

            // 3. Draw Border
            Color borderColor = Color.Black;
            if (card.IsHovered) borderColor = Color.Yellow;
            DrawBorder(sb, card.Bounds, borderColor, 2);

            // 4. Draw Header (Name & Cost)
            Vector2 pos = new Vector2(card.Bounds.X + 5, card.Bounds.Y + 5);
            sb.DrawString(_font, card.Name, pos, Color.Black);

            if (card.Cost > 0)
            {
                string costText = $"Cost: {card.Cost}";

                // Measure the text so we can align it to the bottom-right
                Vector2 costSize = _font.MeasureString(costText);

                // X = Right edge - text width - padding
                // Y = Bottom edge - text height - padding
                Vector2 costPos = new Vector2(
                    card.Bounds.Right - costSize.X - 5,
                    card.Bounds.Bottom - costSize.Y - 5
                );

                sb.DrawString(_font, costText, costPos, Color.DarkBlue);
            }

            // 5. Draw Description / Effects
            float yOffset = 30;
            foreach (var effect in card.Effects)
            {
                string text = GetEffectText(effect);
                sb.DrawString(_font, text, new Vector2(card.Bounds.X + 5, card.Bounds.Y + yOffset), Color.Black, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0);
                yOffset += 15;
            }

            // 6. Draw Stats (DeckVP & InnerCircleVP) [UPDATED]
            // We check if either VP type exists and format a string accordingly
            if (card.DeckVP > 0 || card.InnerCircleVP > 0)
            {
                string vpText;

                if (card.DeckVP > 0 && card.InnerCircleVP > 0)
                {
                    // If card has both, show both compactly
                    vpText = $"D:{card.DeckVP} I:{card.InnerCircleVP}";
                }
                else if (card.DeckVP > 0)
                {
                    vpText = $"VP: {card.DeckVP}"; // Standard VP usually implies Deck VP
                }
                else
                {
                    vpText = $"Inner: {card.InnerCircleVP}";
                }

                // Measure text
                Vector2 vpSize = _font.MeasureString(vpText);

                // Position: Bottom Left (same logic as before, but using new text)
                Vector2 vpPos = new Vector2(
                    card.Bounds.X + 5,
                    card.Bounds.Bottom - vpSize.Y - 5
                );

                sb.DrawString(_font, vpText, vpPos, Color.DarkRed);
            }
        }

        private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
        {
            sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(_pixelTexture, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private Color GetAspectColor(CardAspect aspect)
        {
            return aspect switch
            {
                CardAspect.Warlord => Color.IndianRed, // Red-ish
                CardAspect.Sorcery => Color.MediumPurple, // Purple-ish
                CardAspect.Shadow => Color.CadetBlue, // Blue-ish
                CardAspect.Order => Color.Goldenrod, // Gold-ish
                _ => Color.LightGray
            };
        }

        private string GetEffectText(CardEffect effect)
        {
            // Simple text generation for visualization
            if (effect.Type == EffectType.GainResource) return $"+{effect.Amount} {effect.TargetResource}";
            if (effect.Type == EffectType.Assassinate) return "Assassinate";
            if (effect.Type == EffectType.DeployUnit) return "Deploy";
            if (effect.Type == EffectType.Supplant) return "Supplant";
            if (effect.Type == EffectType.PlaceSpy) return "Place Spy";
            if (effect.Type == EffectType.ReturnUnit) return "Return Unit";
            return effect.Type.ToString();
        }
    }
}