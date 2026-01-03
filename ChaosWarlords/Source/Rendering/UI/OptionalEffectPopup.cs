using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System;

namespace ChaosWarlords.Source.Rendering.UI
{
    /// <summary>
    /// UI popup for optional card effects.
    /// Displays "Do you want to [effect]?" with Yes/No buttons.
    /// </summary>
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

            _popupRect = new Rectangle(popupX, popupY, PopupWidth, PopupHeight);

            // Semi-transparent overlay (darken background)
            Rectangle fullScreen = new Rectangle(0, 0, screenWidth, screenHeight);
            spriteBatch.Draw(whitePixel, fullScreen, Color.Black * 0.6f);

            // Popup background
            spriteBatch.Draw(whitePixel, _popupRect, new Color(40, 40, 50));

            // Border
            DrawBorder(spriteBatch, whitePixel, _popupRect, 3, new Color(100, 100, 120));

            // Title (card name)
            string title = _sourceCard?.Name ?? "Card Effect";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2(popupX + (PopupWidth - titleSize.X) / 2, popupY + 20);
            spriteBatch.DrawString(font, title, titlePos, Color.Gold);

            // Prompt text
            string prompt = FormatPrompt(_sourceCard!, _effect);
            Vector2 promptSize = font.MeasureString(prompt);
            Vector2 promptPos = new Vector2(popupX + (PopupWidth - promptSize.X) / 2, popupY + 70);
            spriteBatch.DrawString(font, prompt, promptPos, Color.LightGray);

            // Buttons
            int buttonY = popupY + PopupHeight - ButtonHeight - 25;
            int totalButtonWidth = (ButtonWidth * 2) + ButtonSpacing;
            int buttonsStartX = popupX + (PopupWidth - totalButtonWidth) / 2;

            _yesButtonRect = new Rectangle(buttonsStartX, buttonY, ButtonWidth, ButtonHeight);
            _noButtonRect = new Rectangle(buttonsStartX + ButtonWidth + ButtonSpacing, buttonY, ButtonWidth, ButtonHeight);

            // Check hover
            bool yesHovered = _yesButtonRect.Contains(_mousePosition);
            bool noHovered = _noButtonRect.Contains(_mousePosition);

            // Draw Yes button (green)
            Color yesColor = yesHovered ? new Color(0, 200, 0) : new Color(0, 140, 0);
            spriteBatch.Draw(whitePixel, _yesButtonRect, yesColor);
            DrawBorder(spriteBatch, whitePixel, _yesButtonRect, 2, Color.White);
            
            string yesText = "Yes";
            Vector2 yesTextSize = font.MeasureString(yesText);
            Vector2 yesTextPos = new Vector2(
                _yesButtonRect.X + (_yesButtonRect.Width - yesTextSize.X) / 2,
                _yesButtonRect.Y + (_yesButtonRect.Height - yesTextSize.Y) / 2
            );
            spriteBatch.DrawString(font, yesText, yesTextPos, Color.White);

            // Draw No button (red)
            Color noColor = noHovered ? new Color(200, 0, 0) : new Color(140, 0, 0);
            spriteBatch.Draw(whitePixel, _noButtonRect, noColor);
            DrawBorder(spriteBatch, whitePixel, _noButtonRect, 2, Color.White);
            
            string noText = "No";
            Vector2 noTextSize = font.MeasureString(noText);
            Vector2 noTextPos = new Vector2(
                _noButtonRect.X + (_noButtonRect.Width - noTextSize.X) / 2,
                _noButtonRect.Y + (_noButtonRect.Height - noTextSize.Y) / 2
            );
            spriteBatch.DrawString(font, noText, noTextPos, Color.White);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Texture2D whitePixel, Rectangle rect, int thickness, Color color)
        {
            // Top
            spriteBatch.Draw(whitePixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(whitePixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(whitePixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(whitePixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        private static string FormatPrompt(Card card, CardEffect effect)
        {
            string action = effect.Type switch
            {
                EffectType.Devour => "devour a card",
                EffectType.PlaceSpy => "place a spy",
                EffectType.Promote => "promote a card",
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
                _onAccept?.Invoke();
                Close();
            }
            else if (_noButtonRect.Contains(clickPos))
            {
                _onDecline?.Invoke();
                Close();
            }
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
