using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Systems
{
    public class UIManager
    {
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;
        private Rectangle _marketButtonRect;
        private Rectangle _assassinateButtonRect;

        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        public UIManager(GraphicsDevice graphicsDevice, SpriteFont defaultFont, SpriteFont smallFont)
        {
            _defaultFont = defaultFont;
            _smallFont = smallFont;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            ScreenWidth = graphicsDevice.Viewport.Width;
            ScreenHeight = graphicsDevice.Viewport.Height;

            // Define Market Button Position (Left Edge, Centered)
            int btnHeight = 100;
            _marketButtonRect = new Rectangle(0, (ScreenHeight / 2) - (btnHeight / 2), 40, btnHeight);
            _assassinateButtonRect = new Rectangle(ScreenWidth - 40, (ScreenHeight / 2) - (btnHeight / 2), 40, btnHeight);
        }

        // LOGIC: Did we click the toggle button?
        public bool IsMarketButtonHovered(InputManager input)
        {
            return input.IsMouseOver(_marketButtonRect);
        }

        public bool IsAssassinateButtonHovered(InputManager input)
        {
            return input.IsMouseOver(_assassinateButtonRect);
        }

        // DRAWING METHODS
        public void DrawTopBar(SpriteBatch spriteBatch, Player player)
        {
            if (_defaultFont == null) return;

            // Background
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, ScreenWidth, 40), Color.Black * 0.5f);

            // Stats (Left)
            spriteBatch.DrawString(_defaultFont, $"Power: {player.Power}", new Vector2(20, 10), Color.Orange);
            spriteBatch.DrawString(_defaultFont, $"Influence: {player.Influence}", new Vector2(150, 10), Color.Cyan);
            spriteBatch.DrawString(_defaultFont, $"VP: {player.VictoryPoints}", new Vector2(300, 10), Color.Lime);

            // Kill count
            spriteBatch.DrawString(_defaultFont, $"Trophies: {player.TrophyHall}", new Vector2(350, 10), Color.Red);

            // Deck Info (Center-ish)
            spriteBatch.DrawString(_defaultFont, $"Deck: {player.Deck.Count}", new Vector2(450, 10), Color.White);
            spriteBatch.DrawString(_defaultFont, $"Discard: {player.DiscardPile.Count}", new Vector2(550, 10), Color.Gray);

            // --- NEW: Troops Counter (Right Corner) ---
            string troopsText = $"Troops: {player.TroopsInBarracks} / 40";
            Vector2 textSize = _defaultFont.MeasureString(troopsText);

            // Position: ScreenWidth - TextWidth - Padding
            float rightX = ScreenWidth - textSize.X - 20;

            Color troopColor = (player.TroopsInBarracks == 0) ? Color.Red : Color.LightGreen;
            spriteBatch.DrawString(_defaultFont, troopsText, new Vector2(rightX, 10), troopColor);
        }

        public void DrawMarketButton(SpriteBatch spriteBatch, bool isOpen)
        {
            spriteBatch.Draw(_pixelTexture, _marketButtonRect, isOpen ? Color.Gray : Color.Gold);

            SpriteFont btnFont = _smallFont ?? _defaultFont;
            if (btnFont != null)
            {
                string btnText = "M\nA\nR\nK\nE\nT";
                Vector2 textSize = btnFont.MeasureString(btnText);

                // Centering math
                float textX = _marketButtonRect.X + (_marketButtonRect.Width - textSize.X) / 2;
                float textY = _marketButtonRect.Y + (_marketButtonRect.Height - textSize.Y) / 2;

                spriteBatch.DrawString(btnFont, btnText, new Vector2(textX, textY), Color.Black);
            }
        }

        public void DrawAssassinateButton(SpriteBatch spriteBatch, Player player)
        {
            bool canAfford = player.Power >= 3;
            Color btnColor = canAfford ? Color.Red : Color.DarkRed * 0.5f;

            spriteBatch.Draw(_pixelTexture, _assassinateButtonRect, btnColor);
            DrawVerticalText(spriteBatch, "K\nI\nL\nL\n\n3", _assassinateButtonRect);
        }

        private void DrawVerticalText(SpriteBatch spriteBatch, string text, Rectangle rect)
        {
            SpriteFont btnFont = _smallFont ?? _defaultFont;
            if (btnFont != null)
            {
                Vector2 textSize = btnFont.MeasureString(text);
                float textX = rect.X + (rect.Width - textSize.X) / 2;
                float textY = rect.Y + (rect.Height - textSize.Y) / 2;
                spriteBatch.DrawString(btnFont, text, new Vector2(textX, textY), Color.Black);
            }
        }

        public void DrawMarketOverlay(SpriteBatch spriteBatch)
        {
            // Dimmer
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, ScreenWidth, ScreenHeight), Color.Black * 0.7f);

            if (_defaultFont != null)
            {
                string title = "MARKET (Buy Cards)";
                Vector2 size = _defaultFont.MeasureString(title);
                spriteBatch.DrawString(_defaultFont, title, new Vector2((ScreenWidth - size.X) / 2, 20), Color.Gold);
            }
        }
    }
}