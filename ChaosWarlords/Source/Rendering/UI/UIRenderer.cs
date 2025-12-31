using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities.Actors;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System;

namespace ChaosWarlords.Source.Views
{
    [ExcludeFromCodeCoverage]
    public class UIRenderer : IDisposable
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
            if (_defaultFont is null) return;

            // 1. Draw Background
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, 40), Color.Black * 0.9f);
            DrawBorder(spriteBatch, _pixelTexture, new Rectangle(0, 0, screenWidth, 40), 1, Color.DarkGray * 0.5f);

            // ====================================================
            // SECTION 1: ECONOMY & SCORE (Left Aligned)
            // ====================================================
            int leftX = 20;
            DrawStat(spriteBatch, "Influence", player.Influence.ToString(CultureInfo.InvariantCulture), Color.Cyan, ref leftX);
            DrawStat(spriteBatch, "Power", player.Power.ToString(CultureInfo.InvariantCulture), Color.Orange, ref leftX);
            DrawStat(spriteBatch, "VP", player.VictoryPoints.ToString(CultureInfo.InvariantCulture), Color.Gold, ref leftX);

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
            DrawRightAlignedStat(spriteBatch, "Deck", player.Deck.Count.ToString(CultureInfo.InvariantCulture), Color.White, ref rightX);

            // Discard (Gray)
            DrawRightAlignedStat(spriteBatch, "Discard", player.DiscardPile.Count.ToString(CultureInfo.InvariantCulture), Color.Gray, ref rightX);

            // Inner Circle (Purple)
            DrawRightAlignedStat(spriteBatch, "Inner Circle", player.InnerCircle.Count.ToString(CultureInfo.InvariantCulture), Color.MediumPurple, ref rightX);
        }

        public void DrawActionButtons(SpriteBatch spriteBatch, IUIManager ui, Player player)
        {
            if (_smallFont is null) return;

            // ASSASSINATE (Right Side - Vertical)
            bool canAffordAssassinate = player.Power >= 3;
            DrawVerticalButton(spriteBatch, ui.AssassinateButtonRect, "ASSASSINATE", ui.IsAssassinateHovered, canAffordAssassinate, Color.Red);

            // RETURN SPY (Right Side - Vertical)
            bool canAffordReturn = player.Power >= 3;
            DrawVerticalButton(spriteBatch, ui.ReturnSpyButtonRect, "RETURN SPY", ui.IsReturnSpyHovered, canAffordReturn, Color.CornflowerBlue);
        }

        public void DrawMarketButton(SpriteBatch spriteBatch, IUIManager ui)
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

        public void DrawPauseMenu(SpriteBatch sb, IUIManager ui)
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

        public void DrawVictoryPopup(SpriteBatch sb, ChaosWarlords.Source.Core.Data.Dtos.VictoryDto victoryData, int screenWidth, int screenHeight)
        {
            if (victoryData == null || !victoryData.IsGameOver) return;

            // 1. Dark Overlay covering entire screen
            sb.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black * 0.9f);

            // 2. Victory Header
            string headerText = $"VICTOR: {victoryData.WinnerName?.ToUpper(CultureInfo.InvariantCulture) ?? "UNKNOWN"}";
            string totalVPText = "TOTAL VP: " + (victoryData.WinnerSeat.HasValue ? victoryData.FinalScores[victoryData.WinnerSeat.Value] : 0);

            // Calculate positions to center header
            Vector2 headerSize = _defaultFont.MeasureString(headerText);
            Vector2 totalSize = _defaultFont.MeasureString(totalVPText);

            float centerX = screenWidth / 2f;
            float topY = 100f;

            // Draw Header
            sb.DrawString(_defaultFont, headerText, new Vector2(centerX - headerSize.X / 2, topY), Color.Gold);
            sb.DrawString(_defaultFont, totalVPText, new Vector2(centerX - totalSize.X / 2, topY + 40), Color.Gold);

            // 3. Draw Winner Score Breakdown (Large)
            if (victoryData.WinnerSeat.HasValue && victoryData.ScoreBreakdowns.TryGetValue(victoryData.WinnerSeat.Value, out var winnerBreakdown))
            {
                Color winnerColor = GetPlayerColor(victoryData.PlayerColors, victoryData.WinnerSeat.Value);
                DrawScoreBreakdown(sb, winnerBreakdown, new Vector2(centerX, topY + 100), true, "", winnerColor);
            }

            // 4. Draw Other Players (Row beneath)
            float otherPlayersY = topY + 300f;
            float gap = 250f;
            
            // Filter out winner
            var otherPlayers = victoryData.ScoreBreakdowns.Keys
                .Where(seat => seat != victoryData.WinnerSeat)
                .OrderBy(seat => seat) // Just stable order
                .ToList();

            if (otherPlayers.Count > 0)
            {
                // Calculate total width of the row to center it
                float totalRowWidth = (otherPlayers.Count * 200f) + ((otherPlayers.Count - 1) * 50f);
                float startX = centerX - (totalRowWidth / 2) + 100f; // Adjusted for center origin

                for (int i = 0; i < otherPlayers.Count; i++)
                {
                    int seat = otherPlayers[i];
                    var breakdown = victoryData.ScoreBreakdowns[seat];
                    Color pColor = GetPlayerColor(victoryData.PlayerColors, seat);
                    string pColorName = "UNKNOWN";
                    if (victoryData.PlayerColors != null && victoryData.PlayerColors.TryGetValue(seat, out var mappedName))
                    {
                        pColorName = mappedName.ToUpper(CultureInfo.InvariantCulture);
                    }
                    string name = $"PLAYER {pColorName}"; 
                    
                    DrawScoreBreakdown(sb, breakdown, new Vector2(startX + (i * gap), otherPlayersY), false, name, pColor);
                }
            }
        }

        private static Color GetPlayerColor(Dictionary<int, string>? colorMap, int seat)
        {
            if (colorMap != null && colorMap.TryGetValue(seat, out var colorName))
            {
                // Simple mapping for standard colors. 
                // Ideally we'd have a shared utility, but UI rendering often does its own mapping for visual tweaks.
                return colorName.ToUpperInvariant() switch
                {
                    "RED" => Color.Red,
                    "BLUE" => Color.Cyan, // Cyan often looks better than deep Blue against dark background
                    "GREEN" => Color.Green,
                    "YELLOW" => Color.Yellow,
                    _ => Color.White
                };
            }
            return Color.White;
        }

        private void DrawScoreBreakdown(SpriteBatch sb, ChaosWarlords.Source.Core.Data.Dtos.ScoreBreakdownDto breakdown, Vector2 centerPos, bool isWinner, string playerName, Color playerColor)
        {
            float scale = isWinner ? 1.0f : 0.8f;
            // Use player color for Winner title too, or keep Gold? User said: "other players ... match the victors in color". 
            // The victor was Gold. The prompt implies using the Player's faction color.
            // Let's use Gold for Winner Title to keep it special, BUT use player color for Name/Stats? 
            // Or use Player Color instead of Gold? 
            // "Victor: Player Blue" -> user used Gold.
            // Let's stick to Gold for main "VICTOR" label, but use styled color for the Breakdown sections if requested.
            // User: "can we maybe make the other players name and total vp match the victors in color?"
            // This implies the other players currently DON'T match the victor (who is colored).
            // So we should colorize the others.
            
            Color titleColor = isWinner ? Color.Gold : playerColor; 
            Color textColor = Color.LightGray;

            int yOffset = 0;
            int lineHeight = (int)(30 * scale);

            if (!isWinner)
            {
                // Draw Name and Total VP for others with their COLOR
                Vector2 nameSize = _defaultFont.MeasureString(playerName);
                sb.DrawString(_defaultFont, playerName, new Vector2(centerPos.X - (nameSize.X * scale) / 2, centerPos.Y + yOffset), titleColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                yOffset += lineHeight;

                string totalText = $"TOTAL VP: {breakdown.TotalScore}";
                Vector2 totalSize = _defaultFont.MeasureString(totalText);
                sb.DrawString(_defaultFont, totalText, new Vector2(centerPos.X - (totalSize.X * scale) / 2, centerPos.Y + yOffset), titleColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                yOffset += lineHeight + 10;
            }

            // Draw Segments
            DrawSegmentLine(sb, "VP Tokens", breakdown.VPTokens, centerPos, ref yOffset, scale, textColor);
            DrawSegmentLine(sb, "Sites", breakdown.SiteControlVP, centerPos, ref yOffset, scale, textColor);
            DrawSegmentLine(sb, "Trophies", breakdown.TrophyHallVP, centerPos, ref yOffset, scale, textColor);
            DrawSegmentLine(sb, "Deck", breakdown.DeckVP, centerPos, ref yOffset, scale, textColor);
            DrawSegmentLine(sb, "Inner Circle", breakdown.InnerCircleVP, centerPos, ref yOffset, scale, textColor);
        }

        private void DrawSegmentLine(SpriteBatch sb, string label, int value, Vector2 centerPos, ref int yOffset, float scale, Color color)
        {
            string text = $"{label}: {value}";
            Vector2 size = _defaultFont.MeasureString(text);
            sb.DrawString(_defaultFont, text, new Vector2(centerPos.X - (size.X * scale) / 2, centerPos.Y + yOffset), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            yOffset += (int)(30 * scale);
        }

        public void Dispose()
        {
            _pixelTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}


