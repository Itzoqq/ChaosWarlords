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
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;
        private int _screenWidth;

        public UIRenderer(GraphicsDevice graphicsDevice, SpriteFont defaultFont, SpriteFont smallFont)
        {
            _defaultFont = defaultFont;
            _smallFont = smallFont;
            _screenWidth = graphicsDevice.Viewport.Width;

            // Create the generic texture here, purely for visual drawing
            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public void Draw(SpriteBatch sb, UIManager ui, Player player, bool isMarketOpen)
        {
            DrawTopBar(sb, player);
            DrawActionButtons(sb, ui, player);
            DrawMarketButton(sb, ui, isMarketOpen);

            if (isMarketOpen)
            {
                DrawMarketOverlay(sb, ui);
            }
        }

        private void DrawTopBar(SpriteBatch sb, Player player)
        {
            // Background
            sb.Draw(_pixelTexture, new Rectangle(0, 0, _screenWidth, 40), Color.Black * 0.5f);

            // Stats (Left)
            DrawString(sb, $"Power: {player.Power}", new Vector2(20, 10), Color.Orange);
            DrawString(sb, $"Influence: {player.Influence}", new Vector2(150, 10), Color.Cyan);
            DrawString(sb, $"VP: {player.VictoryPoints}", new Vector2(300, 10), Color.Lime);
            DrawString(sb, $"Trophies: {player.TrophyHall}", new Vector2(400, 10), Color.Red);

            // Deck Info (Center)
            DrawString(sb, $"Deck: {player.Deck.Count}", new Vector2(500, 10), Color.White);
            DrawString(sb, $"Discard: {player.DiscardPile.Count}", new Vector2(600, 10), Color.Gray);

            // Supplies (Right)
            string troopsText = $"Troops: {player.TroopsInBarracks} / 40";
            Vector2 troopsSize = _defaultFont.MeasureString(troopsText);
            float troopsX = _screenWidth - troopsSize.X - 20;
            Color troopColor = (player.TroopsInBarracks == 0) ? Color.Red : Color.LightGreen;
            DrawString(sb, troopsText, new Vector2(troopsX, 10), troopColor);

            string spiesText = $"Spies: {player.SpiesInBarracks} / 5";
            Vector2 spiesSize = _defaultFont.MeasureString(spiesText);
            float spiesX = troopsX - spiesSize.X - 30;
            Color spyColor = (player.SpiesInBarracks == 0) ? Color.Red : Color.Violet;
            DrawString(sb, spiesText, new Vector2(spiesX, 10), spyColor);
        }

        private void DrawMarketButton(SpriteBatch sb, UIManager ui, bool isOpen)
        {
            // We ask the UI Manager for the Rectangle, but WE decide the color
            sb.Draw(_pixelTexture, ui.MarketButtonRect, isOpen ? Color.Gray : Color.Gold);
            DrawCenteredText(sb, "M\nA\nR\nK\nE\nT", ui.MarketButtonRect, Color.Black);
        }

        private void DrawActionButtons(SpriteBatch sb, UIManager ui, Player player)
        {
            // Assassinate
            bool canAffordKill = player.Power >= 3;
            sb.Draw(_pixelTexture, ui.AssassinateButtonRect, canAffordKill ? Color.Red : Color.DarkRed * 0.5f);
            DrawCenteredText(sb, "K\nI\nL\nL\n\n3", ui.AssassinateButtonRect, Color.Black);

            // Return Spy
            bool canAffordSpy = player.Power >= 3;
            sb.Draw(_pixelTexture, ui.ReturnSpyButtonRect, canAffordSpy ? Color.Violet : Color.Purple * 0.5f);
            DrawCenteredText(sb, "H\nU\nN\nT\n\n3", ui.ReturnSpyButtonRect, Color.Black);
        }

        private void DrawMarketOverlay(SpriteBatch sb, UIManager ui)
        {
            // Dimmer
            sb.Draw(_pixelTexture, new Rectangle(0, 0, _screenWidth, 2000), Color.Black * 0.7f); // Hacky full screen

            string title = "MARKET (Buy Cards)";
            Vector2 size = _defaultFont.MeasureString(title);
            sb.DrawString(_defaultFont, title, new Vector2((_screenWidth - size.X) / 2, 20), Color.Gold);
        }

        // Helpers
        private void DrawString(SpriteBatch sb, string text, Vector2 pos, Color color)
        {
            sb.DrawString(_defaultFont, text, pos, color);
        }

        private void DrawCenteredText(SpriteBatch sb, string text, Rectangle rect, Color color)
        {
            SpriteFont font = _smallFont ?? _defaultFont;
            Vector2 size = font.MeasureString(text);
            float x = rect.X + (rect.Width - size.X) / 2;
            float y = rect.Y + (rect.Height - size.Y) / 2;
            sb.DrawString(font, text, new Vector2(x, y), color);
        }
    }
}