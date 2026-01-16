using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.UI
{
    [ExcludeFromCodeCoverage]
    public class SimpleButton
    {
        public Rectangle Bounds { get; private set; }
        public string Text { get; private set; }
        public Action OnClick { get; private set; }
        public Color NormalColor { get; set; } = Color.White;
        public Color HoverColor { get; set; } = Color.LightGreen;

        public bool IsHovered { get; private set; }

        public SimpleButton(Rectangle bounds, string text, Action onClick)
        {
            Bounds = bounds;
            Text = text;
            OnClick = onClick;
        }

        public void Update(Point mousePosition)
        {
            IsHovered = Bounds.Contains(mousePosition);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            Color color = IsHovered ? HoverColor : NormalColor;

            // Draw background with transparency
            spriteBatch.Draw(pixelTexture, Bounds, Color.Black * 0.5f);

            // Draw Border - pooled rectangle (0 allocations vs 4)
            int border = 2;
            using var borderRect = PooledRectangle.Rent(0, 0, 0, 0);

            borderRect.Value = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, border);
            spriteBatch.Draw(pixelTexture, borderRect.Value, color);

            borderRect.Value = new Rectangle(Bounds.X, Bounds.Y + Bounds.Height - border, Bounds.Width, border);
            spriteBatch.Draw(pixelTexture, borderRect.Value, color);

            borderRect.Value = new Rectangle(Bounds.X, Bounds.Y, border, Bounds.Height);
            spriteBatch.Draw(pixelTexture, borderRect.Value, color);

            borderRect.Value = new Rectangle(Bounds.X + Bounds.Width - border, Bounds.Y, border, Bounds.Height);
            spriteBatch.Draw(pixelTexture, borderRect.Value, color);

            if (font is not null)
            {
                Vector2 textSize = font.MeasureString(Text);
                using var textPos = PooledVector2.Rent(
                    Bounds.X + (Bounds.Width - textSize.X) / 2,
                    Bounds.Y + (Bounds.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(font, Text, textPos.Value, color);
            }
        }
    }
}


