using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

            // Draw Border
            int border = 2;
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, border), color); // Top
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X, Bounds.Y + Bounds.Height - border, Bounds.Width, border), color); // Bottom
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X, Bounds.Y, border, Bounds.Height), color); // Left
            spriteBatch.Draw(pixelTexture, new Rectangle(Bounds.X + Bounds.Width - border, Bounds.Y, border, Bounds.Height), color); // Right

            if (font != null)
            {
                Vector2 textSize = font.MeasureString(Text);
                Vector2 textPos = new Vector2(
                    Bounds.X + (Bounds.Width - textSize.X) / 2,
                    Bounds.Y + (Bounds.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(font, Text, textPos, color);
            }
        }
    }
}


