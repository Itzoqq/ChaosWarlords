using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Systems
{
    [ExcludeFromCodeCoverage]
    public class UIManager
    {
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;

        // Buttons
        private Rectangle _marketButtonRect;
        private Rectangle _assassinateButtonRect;
        private Rectangle _returnSpyButtonRect;

        // --- OPTIMIZATION: String Caches ---
        // These replace the string interpolations in Draw()
        private CachedIntText _powerCache;
        private CachedIntText _influenceCache;
        private CachedIntText _vpCache;
        private CachedIntText _trophyCache;
        private CachedIntText _deckCache;
        private CachedIntText _discardCache;
        private CachedIntText _troopsCache;
        private CachedIntText _spiesCache;

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

            InitializeLayout();
            InitializeStringCaches();
        }

        private void InitializeLayout()
        {
            int btnHeight = 100;
            int btnWidth = 40;
            int verticalGap = 25;

            _marketButtonRect = new Rectangle(0, (ScreenHeight / 2) - (btnHeight / 2), btnWidth, btnHeight);

            _assassinateButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) - btnHeight - verticalGap,
                btnWidth,
                btnHeight
            );

            _returnSpyButtonRect = new Rectangle(
                ScreenWidth - btnWidth,
                (ScreenHeight / 2) + verticalGap,
                btnWidth,
                btnHeight
            );
        }

        private void InitializeStringCaches()
        {
            // Initialize with prefixes. The values will update on the first Draw frame.
            _powerCache = new CachedIntText("Power: ");
            _influenceCache = new CachedIntText("Influence: ");
            _vpCache = new CachedIntText("VP: ");
            _trophyCache = new CachedIntText("Trophies: ");
            _deckCache = new CachedIntText("Deck: ");
            _discardCache = new CachedIntText("Discard: ");
            _troopsCache = new CachedIntText("Troops: ", -1, " / 40");
            _spiesCache = new CachedIntText("Spies: ", -1, " / 5");
        }

        // LOGIC methods remain the same
        public bool IsMarketButtonHovered(InputManager input) => input.IsMouseOver(_marketButtonRect);
        public bool IsAssassinateButtonHovered(InputManager input) => input.IsMouseOver(_assassinateButtonRect);
        public bool IsReturnSpyButtonHovered(InputManager input) => input.IsMouseOver(_returnSpyButtonRect);

        // DRAWING METHODS
        public void DrawTopBar(SpriteBatch spriteBatch, Player player)
        {
            if (_defaultFont == null) return;

            // 1. Update Caches (Only rebuilds string if numbers changed)
            _powerCache.Update(player.Power);
            _influenceCache.Update(player.Influence);
            _vpCache.Update(player.VictoryPoints);
            _trophyCache.Update(player.TrophyHall);
            _deckCache.Update(player.Deck.Count);
            _discardCache.Update(player.DiscardPile.Count);
            _troopsCache.Update(player.TroopsInBarracks);
            _spiesCache.Update(player.SpiesInBarracks);

            // 2. Draw using StringBuilder (Zero Allocation)
            // Background
            spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, ScreenWidth, 40), Color.Black * 0.5f);

            // Stats (Left)
            spriteBatch.DrawString(_defaultFont, _powerCache.Output, new Vector2(20, 10), Color.Orange);
            spriteBatch.DrawString(_defaultFont, _influenceCache.Output, new Vector2(150, 10), Color.Cyan);
            spriteBatch.DrawString(_defaultFont, _vpCache.Output, new Vector2(300, 10), Color.Lime);

            // Kill count
            spriteBatch.DrawString(_defaultFont, _trophyCache.Output, new Vector2(400, 10), Color.Red);

            // Deck Info (Center-ish)
            spriteBatch.DrawString(_defaultFont, _deckCache.Output, new Vector2(500, 10), Color.White);
            spriteBatch.DrawString(_defaultFont, _discardCache.Output, new Vector2(600, 10), Color.Gray);

            // --- RIGHT SIDE: SUPPLIES ---

            // 1. Troops Counter
            Vector2 troopsSize = _defaultFont.MeasureString(_troopsCache.Output);
            float troopsX = ScreenWidth - troopsSize.X - 20;
            Color troopColor = (player.TroopsInBarracks == 0) ? Color.Red : Color.LightGreen;
            spriteBatch.DrawString(_defaultFont, _troopsCache.Output, new Vector2(troopsX, 10), troopColor);

            // 2. Spies Counter
            Vector2 spiesSize = _defaultFont.MeasureString(_spiesCache.Output);
            float spiesX = troopsX - spiesSize.X - 30;
            Color spyColor = (player.SpiesInBarracks == 0) ? Color.Red : Color.Violet;
            spriteBatch.DrawString(_defaultFont, _spiesCache.Output, new Vector2(spiesX, 10), spyColor);
        }

        public void DrawMarketButton(SpriteBatch spriteBatch, bool isOpen)
        {
            spriteBatch.Draw(_pixelTexture, _marketButtonRect, isOpen ? Color.Gray : Color.Gold);

            SpriteFont btnFont = _smallFont ?? _defaultFont;
            if (btnFont != null)
            {
                // This string is a constant literal, so it doesn't allocate new memory at runtime.
                string btnText = "M\nA\nR\nK\nE\nT";
                Vector2 textSize = btnFont.MeasureString(btnText);

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

            // 2. Draw Return Spy Button
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