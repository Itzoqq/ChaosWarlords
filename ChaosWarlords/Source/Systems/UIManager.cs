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
        private Rectangle _returnSpyButtonRect;

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

            int btnHeight = 100;
            int btnWidth = 40;
            int verticalGap = 25; // Add a explicit gap between center and buttons

            // 1. Market (Left - Centered)
            _marketButtonRect = new Rectangle(0, (ScreenHeight / 2) - (btnHeight / 2), btnWidth, btnHeight);

            // 2. Assassinate (Right - Shifted UP by gap)
            _assassinateButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) - btnHeight - verticalGap,
                btnWidth,
                btnHeight
            );

            // 3. Return Spy (Right - Shifted DOWN by gap)
            _returnSpyButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) + verticalGap,
                btnWidth,
                btnHeight
            );
        }

        // LOGIC: Did we click the toggle button?
        public bool IsMarketButtonHovered(InputManager input) => input.IsMouseOver(_marketButtonRect);
        public bool IsAssassinateButtonHovered(InputManager input) => input.IsMouseOver(_assassinateButtonRect);
        public bool IsReturnSpyButtonHovered(InputManager input) => input.IsMouseOver(_returnSpyButtonRect);

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
            spriteBatch.DrawString(_defaultFont, $"Trophies: {player.TrophyHall}", new Vector2(400, 10), Color.Red); // Shifted X slightly to make room

            // Deck Info (Center-ish)
            spriteBatch.DrawString(_defaultFont, $"Deck: {player.Deck.Count}", new Vector2(500, 10), Color.White);
            spriteBatch.DrawString(_defaultFont, $"Discard: {player.DiscardPile.Count}", new Vector2(600, 10), Color.Gray);

            // --- RIGHT SIDE: SUPPLIES ---

            // 1. Troops Counter (Rightmost)
            string troopsText = $"Troops: {player.TroopsInBarracks} / 40";
            Vector2 troopsSize = _defaultFont.MeasureString(troopsText);
            float troopsX = ScreenWidth - troopsSize.X - 20;

            Color troopColor = (player.TroopsInBarracks == 0) ? Color.Red : Color.LightGreen;
            spriteBatch.DrawString(_defaultFont, troopsText, new Vector2(troopsX, 10), troopColor);

            // 2. Spies Counter (To the left of Troops) <--- NEW
            string spiesText = $"Spies: {player.SpiesInBarracks} / 5";
            Vector2 spiesSize = _defaultFont.MeasureString(spiesText);

            // Position: Left of troops text with 30px padding
            float spiesX = troopsX - spiesSize.X - 30;

            Color spyColor = (player.SpiesInBarracks == 0) ? Color.Red : Color.Violet; // Violet for spies (drow theme)
            spriteBatch.DrawString(_defaultFont, spiesText, new Vector2(spiesX, 10), spyColor);
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

        public void DrawActionButtons(SpriteBatch spriteBatch, Player player)
        {
            // 1. Draw Assassinate Button
            bool canAffordKill = player.Power >= 3;
            spriteBatch.Draw(_pixelTexture, _assassinateButtonRect, canAffordKill ? Color.Red : Color.DarkRed * 0.5f);
            DrawVerticalText(spriteBatch, "K\nI\nL\nL\n\n3", _assassinateButtonRect);

            // 2. Draw Return Spy Button <--- NEW
            bool canAffordSpy = player.Power >= 3;
            spriteBatch.Draw(_pixelTexture, _returnSpyButtonRect, canAffordSpy ? Color.Violet : Color.Purple * 0.5f);
            DrawVerticalText(spriteBatch, "H\nU\nN\nT\n\n3", _returnSpyButtonRect);
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