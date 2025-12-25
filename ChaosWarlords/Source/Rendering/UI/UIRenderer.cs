using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Views
{
    [ExcludeFromCodeCoverage]
    public class UIRenderer
    {
        private readonly SpriteFont _defaultFont;
        private readonly SpriteFont _smallFont;
        private readonly Texture2D _pixelTexture;

        public UIRenderer(GraphicsDevice graphicsDevice, SpriteFont defaultFont, SpriteFont smallFont)
        {
            _defaultFont = defaultFont;
            _smallFont = smallFont;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public void DrawTopBar(SpriteBatch spriteBatch, Player player, int screenWidth)
        {
            if (_defaultFont == null) return;

            // 1. Draw Background
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, 40), Color.Black * 0.9f);
            DrawBorder(spriteBatch, _pixelTexture, new Rectangle(0, 0, screenWidth, 40), 1, Color.DarkGray * 0.5f);

            // ====================================================
            // SECTION 1: ECONOMY & SCORE (Left Aligned)
            // ====================================================
            int leftX = 20;
            DrawStat(spriteBatch, "Influence", player.Influence.ToString(), Color.Cyan, ref leftX);
            DrawStat(spriteBatch, "Power", player.Power.ToString(), Color.Orange, ref leftX);
            DrawStat(spriteBatch, "VP", player.VictoryPoints.ToString(), Color.Gold, ref leftX);

            // ====================================================
            // SECTION 2: MILITARY (Centered)
            // ====================================================
            // Uses 'TrophyHall' (int) directly from your Player.cs
            string trophyText = $"Trophies: {player.TrophyHall}";
            string spiesText = $"Spies: {player.SpiesInBarracks}";
            string troopsText = $"Barracks: {player.TroopsInBarracks}";

            // Calculate total width to center the group
            float gap = 30f;
            float totalCenterWidth = _defaultFont.MeasureString(trophyText).X + gap +
                                     _defaultFont.MeasureString(spiesText).X + gap +
                                     _defaultFont.MeasureString(troopsText).X;

            float startX = (screenWidth - totalCenterWidth) / 2;
            int centerX = (int)startX;

            // Draw the Centered Stats
            // Trophies (Pink/Red)
            DrawStatInternal(spriteBatch, trophyText, Color.HotPink, ref centerX, (int)gap);

            // Spies (Blue)
            DrawStatInternal(spriteBatch, spiesText, Color.CornflowerBlue, ref centerX, (int)gap);

            // Troops (Red)
            DrawStatInternal(spriteBatch, troopsText, Color.IndianRed, ref centerX, (int)gap);

            // ====================================================
            // SECTION 3: DECK MANAGEMENT (Right Aligned)
            // ====================================================
            int rightX = screenWidth - 20;

            // Order: Deck -> Discard -> Inner Circle (Draws from Right to Left)

            // Deck (White)
            DrawRightAlignedStat(spriteBatch, "Deck", player.Deck.Count.ToString(), Color.White, ref rightX);

            // Discard (Gray)
            DrawRightAlignedStat(spriteBatch, "Discard", player.DiscardPile.Count.ToString(), Color.Gray, ref rightX);

            // Inner Circle (Purple)
            DrawRightAlignedStat(spriteBatch, "Inner Circle", player.InnerCircle.Count.ToString(), Color.MediumPurple, ref rightX);
        }

        public void DrawActionButtons(SpriteBatch spriteBatch, IUISystem ui, Player player)
        {
            if (_smallFont == null) return;

            // ASSASSINATE (Right Side - Vertical)
            bool canAffordAssassinate = player.Power >= 3;
            DrawVerticalButton(spriteBatch, ui.AssassinateButtonRect, "ASSASSINATE", ui.IsAssassinateHovered, canAffordAssassinate, Color.Red);

            // RETURN SPY (Right Side - Vertical)
            bool canAffordReturn = player.Power >= 3;
            DrawVerticalButton(spriteBatch, ui.ReturnSpyButtonRect, "RETURN SPY", ui.IsReturnSpyHovered, canAffordReturn, Color.CornflowerBlue);
        }

        public void DrawMarketButton(SpriteBatch spriteBatch, IUISystem ui)
        {
            // MARKET (Left Side - Vertical)
            DrawVerticalButton(spriteBatch, ui.MarketButtonRect, "MARKET", ui.IsMarketHovered, true, Color.Gold);
        }

        public void DrawMarketOverlay(SpriteBatch spriteBatch, IMarketManager market, int width, int height)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, width, height), Color.Black * 0.85f);

            string title = "MARKET";
            Vector2 size = _defaultFont.MeasureString(title);
            spriteBatch.DrawString(_defaultFont, title, new Vector2((width - size.X) / 2, 20), Color.Gold);
        }

        // --- HELPERS ---

        private void DrawStat(SpriteBatch sb, string label, string value, Color color, ref int x)
        {
            string text = $"{label}: {value}";
            sb.DrawString(_defaultFont, text, new Vector2(x, 10), color);
            x += (int)_defaultFont.MeasureString(text).X + 30; // Spacing
        }

        private void DrawStatInternal(SpriteBatch sb, string text, Color color, ref int x, int gap)
        {
            sb.DrawString(_defaultFont, text, new Vector2(x, 10), color);
            x += (int)_defaultFont.MeasureString(text).X + gap;
        }

        private void DrawRightAlignedStat(SpriteBatch sb, string label, string value, Color color, ref int rightX)
        {
            string text = $"{label}: {value}";
            Vector2 size = _defaultFont.MeasureString(text);
            rightX -= (int)size.X;
            sb.DrawString(_defaultFont, text, new Vector2(rightX, 10), color);
            rightX -= 30; // Spacing
        }

        private void DrawVerticalButton(SpriteBatch sb, Rectangle rect, string text, bool isHovered, bool isEnabled, Color themeColor)
        {
            Color bgColor;
            Color textColor = Color.Black;

            if (!isEnabled)
            {
                // Background stays dim
                bgColor = Color.DarkGray * 0.5f;

                // Use White or LightGray for readability
                textColor = Color.White;
            }
            else if (isHovered)
            {
                bgColor = themeColor;
                textColor = Color.Black;
            }
            else
            {
                bgColor = Color.Lerp(themeColor, Color.Black, 0.4f);
                textColor = Color.White;
            }

            sb.Draw(_pixelTexture, rect, bgColor);
            UIRenderer.DrawBorder(sb, _pixelTexture, rect, 2, isEnabled ? Color.White : Color.Gray);

            SpriteFont font = _smallFont ?? _defaultFont;
            Vector2 textSize = font.MeasureString(text);
            Vector2 buttonCenter = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Vector2 textOrigin = textSize / 2;

            sb.DrawString(font, text, buttonCenter, textColor, -MathHelper.PiOver2, textOrigin, 1.0f, SpriteEffects.None, 0f);
        }

        public static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        public void DrawHorizontalButton(SpriteBatch sb, Rectangle rect, string text, bool isHovered, bool isEnabled, Color themeColor)
        {
            // Re-use logic or duplicate for horizontal. 
            // Since VerticalButton rotates text, Horizontal won't.
            
            Color bgColor = isEnabled 
                ? (isHovered ? themeColor : Color.Lerp(themeColor, Color.Black, 0.4f))
                : Color.DarkGray * 0.5f;

            Color textColor = (isEnabled && isHovered) ? Color.Black : Color.White;

            sb.Draw(_pixelTexture, rect, bgColor);
            DrawBorder(sb, _pixelTexture, rect, 2, isEnabled ? Color.White : Color.Gray);

            SpriteFont font = _smallFont ?? _defaultFont;
            Vector2 textSize = font.MeasureString(text);
            Vector2 position = new Vector2(
                rect.X + (rect.Width - textSize.X) / 2,
                rect.Y + (rect.Height - textSize.Y) / 2);

            sb.DrawString(font, text, position, textColor);
        }

        public void DrawConfirmationPopup(SpriteBatch sb, string message, Rectangle background, Rectangle confirmBtn, Rectangle cancelBtn, bool confirmHover, bool cancelHover)
        {
            // Dim Background
            // We can't access full screen rect easily here unless passed, but we can draw a large rect?
            // Or just draw the popup box.
            
            // Draw Popup Box
            sb.Draw(_pixelTexture, background, Color.Black * 0.95f);
            DrawBorder(sb, _pixelTexture, background, 2, Color.White);

            // Draw Message
             // Wrap text if needed, but for now simple center
            SpriteFont font = _defaultFont;
            Vector2 textSize = font.MeasureString(message);
            Vector2 msgPos = new Vector2(
                background.X + (background.Width - textSize.X) / 2,
                background.Y + 40);
            
            sb.DrawString(font, message, msgPos, Color.White);

            // Draw Buttons
            DrawHorizontalButton(sb, confirmBtn, "END TURN", confirmHover, true, Color.Red);
            DrawHorizontalButton(sb, cancelBtn, "CANCEL", cancelHover, true, Color.Gray);
        }

        public void DrawPauseMenu(SpriteBatch sb, IUISystem ui)
        {
            // Draw Background
            sb.Draw(_pixelTexture, ui.PauseMenuBackgroundRect, Color.Black * 0.95f);
            DrawBorder(sb, _pixelTexture, ui.PauseMenuBackgroundRect, 2, Color.Cyan);

            // Title
            string title = "PAUSED";
            Vector2 titleSize = _defaultFont.MeasureString(title);
            Vector2 titlePos = new Vector2(
                ui.PauseMenuBackgroundRect.X + (ui.PauseMenuBackgroundRect.Width - titleSize.X) / 2,
                ui.PauseMenuBackgroundRect.Y + 20);
            sb.DrawString(_defaultFont, title, titlePos, Color.Cyan);

            // Buttons
            DrawHorizontalButton(sb, ui.ResumeButtonRect, "RESUME", ui.IsResumeHovered, true, Color.Green);
            DrawHorizontalButton(sb, ui.MainMenuButtonRect, "MAIN MENU", ui.IsMainMenuHovered, true, Color.Orange);
            DrawHorizontalButton(sb, ui.ExitButtonRect, "EXIT", ui.IsExitHovered, true, Color.Red);
        }
        
    }
}