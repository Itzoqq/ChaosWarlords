using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.UI
{
    /// <summary>
    /// A generic modal popup with text and buttons.
    /// Created via PopupBuilder.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Popup
    {
        public string Title { get; }
        public string Message { get; }
        public List<PopupButton> Buttons { get; }

        private Rectangle _bounds;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        public Popup(string title, string message, List<PopupButton> buttons)
        {
            Title = title;
            Message = message;
            Buttons = buttons;
            _isVisible = true;
        }

        public void Close()
        {
            _isVisible = false;
        }

        public void HandleClick(Point mousePos)
        {
            if (!_isVisible) return;

            foreach (var btn in Buttons)
            {
                if (btn.Bounds.Contains(mousePos))
                {
                    btn.OnClick?.Invoke();
                    // Close happens if the action decides to close, or we can auto-close
                    // Usually popups close on action
                    Close();
                    break;
                }
            }
        }

        public void InvokeDefaultAction()
        {
            if (!_isVisible) return;
            var defaultBtn = Buttons.Find(b => b.IsDefault);
            if (defaultBtn != null)
            {
                defaultBtn.OnClick?.Invoke();
                Close();
            }
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel, int screenWidth, int screenHeight, int popupWidth = 400, int popupHeight = 200)
        {
            if (!_isVisible) return;

            // Layout Calculation
            int x = (screenWidth - popupWidth) / 2;
            int y = (screenHeight - popupHeight) / 2;
            _bounds = new Rectangle(x, y, popupWidth, popupHeight);

            // Overlay
            spriteBatch.Draw(whitePixel, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black * 0.7f);

            // Popup Background
            spriteBatch.Draw(whitePixel, _bounds, Color.DarkSlateGray);

            // Title
            Vector2 titleSize = font.MeasureString(Title);
            spriteBatch.DrawString(font, Title, new Vector2(x + (popupWidth - titleSize.X) / 2, y + 20), Color.White);

            // Message
            Vector2 msgSize = font.MeasureString(Message);
            // Center message
            spriteBatch.DrawString(font, Message, new Vector2(x + (popupWidth - msgSize.X) / 2, y + 60), Color.LightGray);

            // Buttons
            int btnWidth = 100;
            int btnHeight = 40;
            int gap = 20;
            int totalBtnWidth = (Buttons.Count * btnWidth) + ((Buttons.Count - 1) * gap);
            int startX = x + (popupWidth - totalBtnWidth) / 2;
            int btnY = y + popupHeight - btnHeight - 20;

            for (int i = 0; i < Buttons.Count; i++)
            {
                var btn = Buttons[i];
                var btnRect = new Rectangle(startX + (i * (btnWidth + gap)), btnY, btnWidth, btnHeight);
                btn.Bounds = btnRect; // Store for click handling

                // Draw Button
                Color color = btn.IsDefault ? Color.DarkGreen : Color.DarkRed; // Simple defaults for now
                // Override for generic buttons? Maybe Gray?
                if (!btn.IsDefault && Buttons.Count > 1) color = Color.Gray;
                // Let's refine colors later or allow Button to specify color.

                spriteBatch.Draw(whitePixel, btnRect, color);

                Vector2 textSize = font.MeasureString(btn.Text);
                Vector2 textPos = new Vector2(btnRect.X + (btnRect.Width - textSize.X) / 2, btnRect.Y + (btnRect.Height - textSize.Y) / 2);
                spriteBatch.DrawString(font, btn.Text, textPos, Color.White);
            }
        }
        public class PopupButton
        {
            public string Text { get; }
            public Action OnClick { get; }
            public bool IsDefault { get; }
            public Rectangle Bounds { get; set; } // Set during Draw/Layout

            public PopupButton(string text, Action onClick, bool isDefault)
            {
                Text = text;
                OnClick = onClick;
                IsDefault = isDefault;
            }
        }
    }
}
