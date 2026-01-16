using ChaosWarlords.Source.Rendering.ViewModels;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.World
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
            if (vm.IsHovered) bgColor = Color.Lerp(bgColor, Color.White, GameConstants.CardRendering.HoverBrighten);

            // 2. Draw Background
            sb.Draw(_pixelTexture, vm.Bounds, bgColor);

            // 3. Draw Border
            Color borderColor = Color.Black;
            if (vm.IsHovered) borderColor = Color.Yellow;
            DrawBorder(sb, vm.Bounds, borderColor, GameConstants.CardRendering.BorderThickness);

            // Use pooled vector for card name position (0 allocations)
            using var namePos = PooledVector2.Rent(vm.Bounds.X + GameConstants.CardRendering.TextPadding, vm.Bounds.Y + GameConstants.CardRendering.TextPadding);
            sb.DrawString(_font, vm.Model.Name, namePos.Value, Color.Black);

            // 5. Draw Cost (Restored to Bottom-Right) - pooled vector (0 allocations)
            if (vm.Model.Cost > 0)
            {
                string costText = $"Cost: {vm.Model.Cost}";
                Vector2 costSize = _font.MeasureString(costText);
                using var costPos = PooledVector2.Rent(vm.Bounds.Right - costSize.X - GameConstants.CardRendering.TextPadding, vm.Bounds.Bottom - costSize.Y - GameConstants.CardRendering.TextPadding);
                sb.DrawString(_font, costText, costPos.Value, Color.Black);
            }

            // 6. Draw Effects - pool outside loop (0 allocations)
            int yOffset = GameConstants.CardRendering.EffectTextStartY;
            using var effectPos = PooledVector2.Rent(0, 0);
            foreach (var effect in vm.Model.Effects)
            {
                string text = GetEffectText(effect);
                effectPos.Value = new Vector2(vm.Bounds.X + GameConstants.CardRendering.TextPadding, vm.Bounds.Y + yOffset);
                sb.DrawString(_font, text, effectPos.Value, Color.Black);
                yOffset += GameConstants.CardRendering.EffectTextSpacing;
            }

            // 7. Draw VPs (Bottom-Left) - pooled vector (0 allocations)
            string vpText = $"D:{vm.Model.DeckVP} I:{vm.Model.InnerCircleVP}";
            using var vpPos = PooledVector2.Rent(vm.Bounds.X + GameConstants.CardRendering.TextPadding, vm.Bounds.Bottom - GameConstants.CardRendering.VictoryPointsOffsetY);
            sb.DrawString(_font, vpText, vpPos.Value, Color.DarkSlateGray);
        }

        private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
        {
            // Use single pooled rectangle, reuse for all 4 sides (0 allocations vs 4)
            using var pooledRect = PooledRectangle.Rent(0, 0, 0, 0);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, rect.Width, thickness);
            sb.Draw(_pixelTexture, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness);
            sb.Draw(_pixelTexture, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, thickness, rect.Height);
            sb.Draw(_pixelTexture, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height);
            sb.Draw(_pixelTexture, pooledRect.Value, color);
        }

        private static Color GetAspectColor(CardAspect aspect)
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

        private static string GetEffectText(CardEffect effect)
        {
            if (effect.Type == EffectType.GainResource) return $"+{effect.Amount} {effect.TargetResource}";
            return effect.Type.ToString();
        }
    }
}


