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

        public void Draw(SpriteBatch sb, CardViewModel vm)
        {
            // 1. Determine Color based on Aspect
            Color bgColor = GetAspectColor(vm.Model.Aspect);
            if (vm.IsHovered) bgColor = Color.Lerp(bgColor, Color.White, 0.3f);

            // 2. Draw Background
            sb.Draw(_pixelTexture, vm.Bounds, bgColor);

            // 3. Draw Border
            Color borderColor = Color.Black;
            if (vm.IsHovered) borderColor = Color.Yellow;
            DrawBorder(sb, vm.Bounds, borderColor, 2);

            // 4. Draw Name (Top-Left)
            Vector2 pos = new Vector2(vm.Bounds.X + 5, vm.Bounds.Y + 5);
            sb.DrawString(_font, vm.Model.Name, pos, Color.Black);

            // 5. Draw Cost (Restored to Bottom-Right)
            if (vm.Model.Cost > 0)
            {
                string costText = $"Cost: {vm.Model.Cost}";
                Vector2 costSize = _font.MeasureString(costText);
                Vector2 costPos = new Vector2(vm.Bounds.Right - costSize.X - 5, vm.Bounds.Bottom - costSize.Y - 5);
                sb.DrawString(_font, costText, costPos, Color.Black);
            }

            // 6. Draw Effects
            int yOffset = 40;
            foreach (var effect in vm.Model.Effects)
            {
                string text = GetEffectText(effect);
                sb.DrawString(_font, text, new Vector2(vm.Bounds.X + 5, vm.Bounds.Y + yOffset), Color.Black);
                yOffset += 20;
            }

            // 7. Draw VPs (Bottom-Left)
            string vpText = $"D:{vm.Model.DeckVP} I:{vm.Model.InnerCircleVP}";
            sb.DrawString(_font, vpText, new Vector2(vm.Bounds.X + 5, vm.Bounds.Bottom - 20), Color.DarkSlateGray);
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
                CardAspect.Warlord => Color.IndianRed,
                CardAspect.Sorcery => Color.MediumPurple,
                CardAspect.Shadow => Color.CadetBlue,
                CardAspect.Order => Color.Goldenrod,
                _ => Color.LightGray
            };
        }

        private string GetEffectText(CardEffect effect)
        {
            if (effect.Type == EffectType.GainResource) return $"+{effect.Amount} {effect.TargetResource}";
            return effect.Type.ToString();
        }
    }
}