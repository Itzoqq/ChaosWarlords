using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ChaosWarlords.Source.Rendering.UI
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

        public void DrawHUD(SpriteBatch spriteBatch, Player player, int screenWidth, IMatchManager matchManager)
        {
            if (_defaultFont is null) return;

            // 1. Draw Background
            // Use pooled rectangle for HUD background (0 allocations)
            using var hudBg = PooledRectangle.Rent(0, 0, screenWidth, GameConstants.UILayout.TopBarHeight);
            spriteBatch.Draw(_pixelTexture, hudBg.Value, Color.Black * 0.9f);
            DrawBorder(spriteBatch, _pixelTexture, hudBg.Value, 1, Color.DarkGray * 0.5f);

            // --- TOP LEFT: Turn Info (BELOW BAR) ---
            int lineHeight = _defaultFont.LineSpacing + GameConstants.UILayout.SmallPadding;
            int yPos = GameConstants.UILayout.TopBarHeight + GameConstants.UILayout.SmallPadding;
            // Draw Round / Turn Counters
            string roundText = $"Round: {matchManager.RoundNumber} | Turn: {matchManager.TotalTurnCount}";
            // Current Player Name below counters
            string playerText = $"{player.DisplayName}'s Turn";

            // Pooled vectors for turn info (0 allocations)
            using var roundPos = PooledVector2.Rent(GameConstants.UILayout.TopBarPadding, yPos);
            spriteBatch.DrawString(_smallFont ?? _defaultFont, roundText, roundPos.Value, Color.LightGray);
            yPos += lineHeight;
            using var playerPos = PooledVector2.Rent(GameConstants.UILayout.TopBarPadding, yPos);
            spriteBatch.DrawString(_defaultFont, playerText, playerPos.Value, player.Color == PlayerColor.Red ? Color.Red : Color.Cyan);

            // ====================================================
            // SECTION 1: ECONOMY & SCORE (Left Aligned - inside Top Bar)
            // ====================================================
            int leftX = GameConstants.UILayout.LargePadding; // Reset to original position
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
            int rightX = screenWidth - GameConstants.UILayout.LargePadding;

            // Order: Deck -> Discard -> Inner Circle (Draws from Right to Left)

            // Deck (White)
            DrawRightAlignedStat(spriteBatch, "Deck", player.Deck.Count.ToString(CultureInfo.InvariantCulture), Color.White, ref rightX);

            // Discard (Gray)
            DrawRightAlignedStat(spriteBatch, "Discard", player.DiscardPile.Count.ToString(CultureInfo.InvariantCulture), Color.Gray, ref rightX);

            // Inner Circle (Purple)
            DrawRightAlignedStat(spriteBatch, "Inner Circle", player.InnerCircle.Count.ToString(CultureInfo.InvariantCulture), Color.MediumPurple, ref rightX);

            // Void / Devour Pile (Plum/DarkRed) - Shared Pile
            DrawRightAlignedStat(spriteBatch, "Void", matchManager.VoidPile.Count.ToString(CultureInfo.InvariantCulture), Color.Plum, ref rightX);

            // ====================================================
            // TROOP DEPLOYMENT INDICATOR (Right side, below bar)
            // ====================================================
            // Only show when player has FREE troops from card effects this turn
            if (player.PendingFreeTroops > 0)
            {
                int rightYPos = GameConstants.UILayout.TopBarHeight + GameConstants.UILayout.SmallPadding;
                string troopDeployText = $"[!] {player.PendingFreeTroops} Free Troops";
                Vector2 textSize = _defaultFont.MeasureString(troopDeployText);
                using var position = PooledVector2.Rent(
                    screenWidth - GameConstants.UILayout.TopBarPadding - textSize.X,
                    rightYPos
                );

                // Draw with pulsing effect to draw attention
                float pulse = (float)Math.Sin(DateTime.Now.Millisecond / 200.0) * 0.3f + 0.7f;
                Color troopColor = Color.LimeGreen * pulse;

                spriteBatch.DrawString(_defaultFont, troopDeployText, position.Value, troopColor);
            }
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
            // Pooled rectangle and vector for market overlay (0 allocations)
            using var marketBg = PooledRectangle.Rent(0, 0, width, height);
            spriteBatch.Draw(_pixelTexture, marketBg.Value, Color.Black * 0.85f);

            string title = "MARKET";
            Vector2 size = _defaultFont.MeasureString(title);
            using var titlePos = PooledVector2.Rent((width - size.X) / 2, GameConstants.UILayout.HeaderTopMargin);
            spriteBatch.DrawString(_defaultFont, title, titlePos.Value, Color.Gold);
        }

        // --- HELPERS ---

        private void DrawStat(SpriteBatch sb, string label, string value, Color color, ref int x)
        {
            string text = $"{label}: {value}";
            using var pos = PooledVector2.Rent(x, GameConstants.UILayout.TopBarPadding);
            sb.DrawString(_defaultFont, text, pos.Value, color);
            x += (int)_defaultFont.MeasureString(text).X + GameConstants.UILayout.TopBarSpacing;
        }

        private void DrawStatInternal(SpriteBatch sb, string text, Color color, ref int x, int gap)
        {
            using var pos = PooledVector2.Rent(x, GameConstants.UILayout.TopBarPadding);
            sb.DrawString(_defaultFont, text, pos.Value, color);
            x += (int)_defaultFont.MeasureString(text).X + gap;
        }

        private void DrawRightAlignedStat(SpriteBatch sb, string label, string value, Color color, ref int rightX)
        {
            string text = $"{label}: {value}";
            Vector2 size = _defaultFont.MeasureString(text);
            rightX -= (int)size.X;
            using var pos = PooledVector2.Rent(rightX, GameConstants.UILayout.TopBarPadding);
            sb.DrawString(_defaultFont, text, pos.Value, color);
            rightX -= GameConstants.UILayout.TopBarSpacing; // Spacing
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
            DrawBorder(sb, _pixelTexture, rect, 2, isEnabled ? Color.White : Color.Gray);

            SpriteFont font = _smallFont ?? _defaultFont;
            Vector2 textSize = font.MeasureString(text);
            using var buttonCenter = PooledVector2.Rent(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            using var textOrigin = PooledVector2.Rent(textSize.X / 2, textSize.Y / 2);

            sb.DrawString(font, text, buttonCenter.Value, textColor, -MathHelper.PiOver2, textOrigin.Value, 1.0f, SpriteEffects.None, 0f);
        }

        public static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            // Use single pooled rectangle, reuse for all 4 sides (0 allocations vs 4)
            using var pooledRect = PooledRectangle.Rent(0, 0, 0, 0);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, rect.Width, thickness);
            spriteBatch.Draw(pixel, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness);
            spriteBatch.Draw(pixel, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X, rect.Y, thickness, rect.Height);
            spriteBatch.Draw(pixel, pooledRect.Value, color);

            pooledRect.Value = new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height);
            spriteBatch.Draw(pixel, pooledRect.Value, color);
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
            using var position = PooledVector2.Rent(
                rect.X + (rect.Width - textSize.X) / 2,
                rect.Y + (rect.Height - textSize.Y) / 2);

            sb.DrawString(font, text, position.Value, textColor);
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
            using var msgPos = PooledVector2.Rent(
                background.X + (background.Width - textSize.X) / 2,
                background.Y + GameConstants.UILayout.DefaultYOffset);

            sb.DrawString(font, message, msgPos.Value, Color.White);

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
            using var titlePos = PooledVector2.Rent(
                ui.PauseMenuBackgroundRect.X + (ui.PauseMenuBackgroundRect.Width - titleSize.X) / 2,
                ui.PauseMenuBackgroundRect.Y + GameConstants.UILayout.HeaderTopMargin);
            sb.DrawString(_defaultFont, title, titlePos.Value, Color.Cyan);

            // Buttons
            DrawHorizontalButton(sb, ui.ResumeButtonRect, "RESUME", ui.IsResumeHovered, true, Color.Green);
            DrawHorizontalButton(sb, ui.MainMenuButtonRect, "MAIN MENU", ui.IsMainMenuHovered, true, Color.Orange);
            DrawHorizontalButton(sb, ui.ExitButtonRect, "EXIT", ui.IsExitHovered, true, Color.Red);
        }

        public void DrawVictoryPopup(SpriteBatch sb, Core.Data.Dtos.VictoryDto victoryData, int screenWidth, int screenHeight)
        {
            if (victoryData == null || !victoryData.IsGameOver) return;

            // 1. Dark Overlay covering entire screen
            // Pooled rectangle for victory overlay (0 allocations)
            using var overlay = PooledRectangle.Rent(0, 0, screenWidth, screenHeight);
            sb.Draw(_pixelTexture, overlay.Value, Color.Black * 0.9f);

            // 2. Victory Header
            string headerText = $"VICTOR: {victoryData.WinnerName?.ToUpper(CultureInfo.InvariantCulture) ?? "UNKNOWN"}";
            string totalVPText = "TOTAL VP: " + (victoryData.WinnerSeat.HasValue ? victoryData.FinalScores[victoryData.WinnerSeat.Value] : 0);

            // Calculate positions to center header
            Vector2 headerSize = _defaultFont.MeasureString(headerText);
            Vector2 totalSize = _defaultFont.MeasureString(totalVPText);

            float centerX = screenWidth / 2f;
            float topY = 100f;

            // Draw Header
            using var headerPos = PooledVector2.Rent(centerX - headerSize.X / 2, topY);
            using var totalPos = PooledVector2.Rent(centerX - totalSize.X / 2, topY + GameConstants.UILayout.DefaultYOffset);
            sb.DrawString(_defaultFont, headerText, headerPos.Value, Color.Gold);
            sb.DrawString(_defaultFont, totalVPText, totalPos.Value, Color.Gold);

            // 3. Draw Winner Score Breakdown (Large)
            if (victoryData.WinnerSeat.HasValue && victoryData.ScoreBreakdowns.TryGetValue(victoryData.WinnerSeat.Value, out var winnerBreakdown))
            {
                Color winnerColor = GetPlayerColor(victoryData.PlayerColors, victoryData.WinnerSeat.Value);
                using var winnerPos = PooledVector2.Rent(centerX, topY + 100);
                DrawScoreBreakdown(sb, winnerBreakdown, winnerPos.Value, true, "", winnerColor);
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

                    using var otherPos = PooledVector2.Rent(startX + (i * gap), otherPlayersY);
                    DrawScoreBreakdown(sb, breakdown, otherPos.Value, false, name, pColor);
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

        private void DrawScoreBreakdown(SpriteBatch sb, Core.Data.Dtos.ScoreBreakdownDto breakdown, Vector2 centerPos, bool isWinner, string playerName, Color playerColor)
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
            int lineHeight = (int)(GameConstants.UILayout.DefaultButtonHeight * scale);

            if (!isWinner)
            {
                // Draw Name and Total VP for others with their COLOR
                Vector2 nameSize = _defaultFont.MeasureString(playerName);
                using var namePos = PooledVector2.Rent(centerPos.X - (nameSize.X * scale) / 2, centerPos.Y + yOffset);
                sb.DrawString(_defaultFont, playerName, namePos.Value, titleColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                yOffset += lineHeight;

                string totalText = $"TOTAL VP: {breakdown.TotalScore}";
                Vector2 totalSize = _defaultFont.MeasureString(totalText);
                using var totalTextPos = PooledVector2.Rent(centerPos.X - (totalSize.X * scale) / 2, centerPos.Y + yOffset);
                sb.DrawString(_defaultFont, totalText, totalTextPos.Value, titleColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                yOffset += lineHeight + GameConstants.UILayout.MediumPadding;
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
            using var segmentPos = PooledVector2.Rent(centerPos.X - (size.X * scale) / 2, centerPos.Y + yOffset);
            sb.DrawString(_defaultFont, text, segmentPos.Value, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            yOffset += (int)(GameConstants.UILayout.DefaultButtonHeight * scale);
        }

        public void Dispose()
        {
            _pixelTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}


