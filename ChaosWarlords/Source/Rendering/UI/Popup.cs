using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Core.Utilities;
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
            // Pooled rectangles for overlay and bounds (0 allocations)
            using var overlay = PooledRectangle.Rent(0, 0, screenWidth, screenHeight);
            using var bounds = PooledRectangle.Rent(x, y, popupWidth, popupHeight);
            _bounds = bounds.Value;

            spriteBatch.Draw(whitePixel, overlay.Value, Color.Black * 0.7f);
            spriteBatch.Draw(whitePixel, bounds.Value, Color.DarkSlateGray);

            // Title - pooled vector (0 allocations)
            Vector2 titleSize = font.MeasureString(Title);
            using var titlePos = PooledVector2.Rent(x + (popupWidth - titleSize.X) / 2, y + 20);
            spriteBatch.DrawString(font, Title, titlePos.Value, Color.White);

            // Message - pooled vector (0 allocations)
            Vector2 msgSize = font.MeasureString(Message);
            using var msgPos = PooledVector2.Rent(x + (popupWidth - msgSize.X) / 2, y + 60);
            spriteBatch.DrawString(font, Message, msgPos.Value, Color.LightGray);

            // Buttons
            int btnWidth = 100;
            int btnHeight = 40;
            int gap = 20;
            int totalBtnWidth = (Buttons.Count * btnWidth) + ((Buttons.Count - 1) * gap);
            int startX = x + (popupWidth - totalBtnWidth) / 2;
            int btnY = y + popupHeight - btnHeight - 20;

            // Pool rectangle and vector outside loop (0 allocations)
            using var btnRect = PooledRectangle.Rent(0, 0, btnWidth, btnHeight);
            using var textPos = PooledVector2.Rent(0, 0);

            for (int i = 0; i < Buttons.Count; i++)
            {
                var btn = Buttons[i];
                btnRect.Value = new Rectangle(startX + (i * (btnWidth + gap)), btnY, btnWidth, btnHeight);
                btn.Bounds = btnRect.Value;

                Color color = btn.IsDefault ? Color.DarkGreen : Color.DarkRed;
                if (!btn.IsDefault && Buttons.Count > 1) color = Color.Gray;

                spriteBatch.Draw(whitePixel, btnRect.Value, color);

                Vector2 textSize = font.MeasureString(btn.Text);
                textPos.Value = new Vector2(btnRect.Value.X + (btnRect.Value.Width - textSize.X) / 2, btnRect.Value.Y + (btnRect.Value.Height - textSize.Y) / 2);
                spriteBatch.DrawString(font, btn.Text, textPos.Value, Color.White);
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
