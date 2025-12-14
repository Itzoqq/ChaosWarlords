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

        // --- PUBLIC API (Callable by GameplayState) ---

        public void DrawTopBar(SpriteBatch spriteBatch, Player player, int screenWidth)
        {
            if (_defaultFont == null) return;

            // Background
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, 40), Color.Black * 0.5f);

            // Stats
            spriteBatch.DrawString(_defaultFont, $"Power: {player.Power}", new Vector2(20, 10), Color.Orange);
            spriteBatch.DrawString(_defaultFont, $"Influence: {player.Influence}", new Vector2(150, 10), Color.Cyan);
            spriteBatch.DrawString(_defaultFont, $"VP: {player.VictoryPoints}", new Vector2(300, 10), Color.Lime);
            spriteBatch.DrawString(_defaultFont, $"Trophies: {player.TrophyHall}", new Vector2(400, 10), Color.Red);

            // Deck Info
            spriteBatch.DrawString(_defaultFont, $"Deck: {player.Deck.Count}", new Vector2(500, 10), Color.White);
            spriteBatch.DrawString(_defaultFont, $"Discard: {player.DiscardPile.Count}", new Vector2(600, 10), Color.Gray);

            // Supplies
            string troopsText = $"Troops: {player.TroopsInBarracks} / 40";
            Vector2 troopsSize = _defaultFont.MeasureString(troopsText);
            float troopsX = screenWidth - troopsSize.X - 20;
            Color troopColor = (player.TroopsInBarracks == 0) ? Color.Red : Color.LightGreen;
            spriteBatch.DrawString(_defaultFont, troopsText, new Vector2(troopsX, 10), troopColor);

            string spiesText = $"Spies: {player.SpiesInBarracks} / 5";
            Vector2 spiesSize = _defaultFont.MeasureString(spiesText);
            float spiesX = troopsX - spiesSize.X - 30;
            Color spyColor = (player.SpiesInBarracks == 0) ? Color.Red : Color.Violet;
            spriteBatch.DrawString(_defaultFont, spiesText, new Vector2(spiesX, 10), spyColor);
        }

        public void DrawActionButtons(SpriteBatch spriteBatch, UIManager ui, Player player)
        {
            // Assassinate
            bool canAffordKill = player.Power >= 3;
            spriteBatch.Draw(_pixelTexture, ui.AssassinateButtonRect, canAffordKill ? Color.Red : Color.DarkRed * 0.5f);
            DrawVerticalText(spriteBatch, "K\nI\nL\nL\n\n3", ui.AssassinateButtonRect);

            // Return Spy
            bool canAffordSpy = player.Power >= 3;
            spriteBatch.Draw(_pixelTexture, ui.ReturnSpyButtonRect, canAffordSpy ? Color.Violet : Color.Purple * 0.5f);
            DrawVerticalText(spriteBatch, "H\nU\nN\nT\n\n3", ui.ReturnSpyButtonRect);
        }

        public void DrawMarketButton(SpriteBatch spriteBatch, UIManager ui, bool isOpen)
        {
            spriteBatch.Draw(_pixelTexture, ui.MarketButtonRect, isOpen ? Color.Gray : Color.Gold);
            DrawVerticalText(spriteBatch, "M\nA\nR\nK\nE\nT", ui.MarketButtonRect);
        }

        public void DrawMarketOverlay(SpriteBatch spriteBatch, int width, int height)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            string title = "MARKET (Buy Cards)";
            Vector2 size = _defaultFont.MeasureString(title);
            spriteBatch.DrawString(_defaultFont, title, new Vector2((width - size.X) / 2, 20), Color.Gold);
        }

        // --- PRIVATE HELPERS (Internal details only) ---

        private void DrawVerticalText(SpriteBatch spriteBatch, string text, Rectangle rect)
        {
            SpriteFont btnFont = _smallFont ?? _defaultFont;
            Vector2 textSize = btnFont.MeasureString(text);
            float textX = rect.X + (rect.Width - textSize.X) / 2;
            float textY = rect.Y + (rect.Height - textSize.Y) / 2;
            spriteBatch.DrawString(btnFont, text, new Vector2(textX, textY), Color.Black);
        }

        // Static helper can be public if used generally, or internal
        public static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}