using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.UI
{
    /// <summary>
    /// UI popup for optional card effects.
    /// Displays "Do you want to [effect]?" with Yes/No buttons.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OptionalEffectPopup
    {
        private Card? _sourceCard;
        private CardEffect? _effect;
        private Action? _onAccept;
        private Action? _onDecline;
        private bool _isVisible;

        // Layout constants
        private const int PopupWidth = 500;
        private const int PopupHeight = 220;
        private const int ButtonWidth = 100;
        private const int ButtonHeight = 45;
        private const int ButtonSpacing = 40;

        // Cached rectangles
        private Rectangle _popupRect;
        private Rectangle _yesButtonRect;
        private Rectangle _noButtonRect;
        private Point _mousePosition;

        public bool IsVisible => _isVisible;

        public void Show(Card card, CardEffect effect, Action onAccept, Action onDecline)
        {
            _sourceCard = card;
            _effect = effect;
            _onAccept = onAccept;
            _onDecline = onDecline;
            _isVisible = true;

            // Calculate centered popup position (will be updated in Draw with screen dimensions)
        }

        public void UpdateMousePosition(Point mousePosition)
        {
            _mousePosition = mousePosition;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel, int screenWidth, int screenHeight)
        {
            if (!_isVisible || _effect == null) return;

            // Calculate centered position
            int popupX = (screenWidth - PopupWidth) / 2;
            int popupY = (screenHeight - PopupHeight) / 2;

            // Pooled rectangles for popup and overlay (0 allocations)
            using var popupRect = PooledRectangle.Rent(popupX, popupY, PopupWidth, PopupHeight);
            using var fullScreen = PooledRectangle.Rent(0, 0, screenWidth, screenHeight);
            _popupRect = popupRect.Value;

            spriteBatch.Draw(whitePixel, fullScreen.Value, Color.Black * 0.6f);
            spriteBatch.Draw(whitePixel, popupRect.Value, new Color(40, 40, 50));
            DrawBorder(spriteBatch, whitePixel, popupRect.Value, 3, new Color(100, 100, 120));

            // Title - pooled vector (0 allocations)
            string title = _sourceCard?.Name ?? "Card Effect";
            Vector2 titleSize = font.MeasureString(title);
            using var titlePos = PooledVector2.Rent(popupX + (PopupWidth - titleSize.X) / 2, popupY + 20);
            spriteBatch.DrawString(font, title, titlePos.Value, Color.Gold);

            // Prompt - pooled vector (0 allocations)
            string prompt = FormatPrompt(_sourceCard!, _effect);
            Vector2 promptSize = font.MeasureString(prompt);
            using var promptPos = PooledVector2.Rent(popupX + (PopupWidth - promptSize.X) / 2, popupY + 70);
            spriteBatch.DrawString(font, prompt, promptPos.Value, Color.LightGray);

            // Buttons
            int buttonY = popupY + PopupHeight - ButtonHeight - 25;
            int totalButtonWidth = (ButtonWidth * 2) + ButtonSpacing;
            int buttonsStartX = popupX + (PopupWidth - totalButtonWidth) / 2;

            // Pooled rectangles for buttons (0 allocations)
            using var yesRect = PooledRectangle.Rent(buttonsStartX, buttonY, ButtonWidth, ButtonHeight);
            using var noRect = PooledRectangle.Rent(buttonsStartX + ButtonWidth + ButtonSpacing, buttonY, ButtonWidth, ButtonHeight);
            _yesButtonRect = yesRect.Value;
            _noButtonRect = noRect.Value;

            bool yesHovered = _yesButtonRect.Contains(_mousePosition);
            bool noHovered = _noButtonRect.Contains(_mousePosition);

            // Draw Yes button - pooled vector (0 allocations)
            Color yesColor = yesHovered ? new Color(0, 200, 0) : new Color(0, 140, 0);
            spriteBatch.Draw(whitePixel, _yesButtonRect, yesColor);
            DrawBorder(spriteBatch, whitePixel, _yesButtonRect, 2, Color.White);

            string yesText = "Yes";
            Vector2 yesTextSize = font.MeasureString(yesText);
            using var yesTextPos = PooledVector2.Rent(
                _yesButtonRect.X + (_yesButtonRect.Width - yesTextSize.X) / 2,
                _yesButtonRect.Y + (_yesButtonRect.Height - yesTextSize.Y) / 2
            );
            spriteBatch.DrawString(font, yesText, yesTextPos.Value, Color.White);

            // Draw No button - pooled vector (0 allocations)
            Color noColor = noHovered ? new Color(200, 0, 0) : new Color(140, 0, 0);
            spriteBatch.Draw(whitePixel, _noButtonRect, noColor);
            DrawBorder(spriteBatch, whitePixel, _noButtonRect, 2, Color.White);

            string noText = "No";
            Vector2 noTextSize = font.MeasureString(noText);
            using var noTextPos = PooledVector2.Rent(
                _noButtonRect.X + (_noButtonRect.Width - noTextSize.X) / 2,
                _noButtonRect.Y + (_noButtonRect.Height - noTextSize.Y) / 2
            );
            spriteBatch.DrawString(font, noText, noTextPos.Value, Color.White);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Texture2D whitePixel, Rectangle rect, int thickness, Color color)
        {
            // Top
            // Use single pooled rectangle, reuse for all 4 sides (0 allocations vs 4)
            using var pooledRect = PooledRectangle.Rent(0, 0, 0, 0);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, rect.Width, thickness);
            spriteBatch.Draw(whitePixel, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness);
            spriteBatch.Draw(whitePixel, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, thickness, rect.Height);
            spriteBatch.Draw(whitePixel, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height);
            spriteBatch.Draw(whitePixel, pooledRect.Value, color);
        }

        private static string FormatPrompt(Card card, CardEffect effect)
        {
            string action = effect.Type switch
            {
                EffectType.Devour => "devour a card",
                EffectType.PlaceSpy => "place a spy",
                EffectType.Promote => "promote a card",
                EffectType.PromoteFromPile => "promote a card",
                EffectType.Assassinate => "assassinate a troop",
                EffectType.MoveUnit => "move a unit",
                EffectType.ReturnUnit => "return a unit",
                _ => effect.Type.ToString().ToLowerInvariant()
            };

            // Add chained effect if present
            if (effect.OnSuccess != null)
            {
                string chainedAction = effect.OnSuccess.Type switch
                {
                    EffectType.Supplant => "supplant a troop",
                    EffectType.GainResource => $"gain {effect.OnSuccess.Amount} {effect.OnSuccess.TargetResource}",
                    EffectType.DrawCard => $"draw {effect.OnSuccess.Amount} card(s)",
                    _ => effect.OnSuccess.Type.ToString().ToLowerInvariant()
                };
                action += $" to {chainedAction}";
            }

            return $"Do you want to {action}?";
        }

        public void HandleClick(int mouseX, int mouseY)
        {
            if (!_isVisible) return;

            Point clickPos = new Point(mouseX, mouseY);

            if (_yesButtonRect.Contains(clickPos))
            {
                InvokeAccept();
            }
            else if (_noButtonRect.Contains(clickPos))
            {
                InvokeDecline();
            }
        }

        public void InvokeAccept()
        {
            if (!_isVisible) return;
            _onAccept?.Invoke();
            Close();
        }

        public void InvokeDecline()
        {
            if (!_isVisible) return;
            _onDecline?.Invoke();
            Close();
        }

        public void ForceClose()
        {
            Close();
        }

        private void Close()
        {
            _isVisible = false;
            _sourceCard = null;
            _effect = null;
            _onAccept = null;
            _onDecline = null;
        }
    }
}
