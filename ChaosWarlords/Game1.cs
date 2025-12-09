using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using System.IO;
using System;

namespace ChaosWarlords
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixelTexture;
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;

        // SYSTEMS
        private MapManager _mapManager;
        private MarketManager _marketManager;
        
        // STATE
        private Player _activePlayer; 
        private bool _wasMousePressed = false; 

        // --- NEW UI STATE ---
        private bool _isMarketOpen = false;
        private Rectangle _marketButtonRect; // The button area

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = Content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }
            try { _smallFont = Content.Load<SpriteFont>("fonts/SmallFont"); } catch { }

            GameLogger.Initialize();

            // PATH FIX: Use BaseDirectory to find the copied json file
            string cardJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            CardDatabase.Load(cardJsonPath, _pixelTexture);

            _marketManager = new MarketManager();
            _marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            _activePlayer = new Player(PlayerColor.Red);
            for(int i=0; i<3; i++) _activePlayer.Deck.Add(CardFactory.CreateSoldier(_pixelTexture));
            for(int i=0; i<7; i++) _activePlayer.Deck.Add(CardFactory.CreateNoble(_pixelTexture));
            
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals(); 

            var nodes = MapFactory.CreateTestMap(_pixelTexture);
            _mapManager = new MapManager(nodes);
            _mapManager.PixelTexture = _pixelTexture;

            // --- UI SETUP ---
            // Create a button on the middle-left edge (Width 40, Height 100)
            _marketButtonRect = new Rectangle(0, (720 / 2) - 50, 40, 100);
        }

        private void ArrangeHandVisuals()
        {
            int startX = 100;
            int gap = 160;
            for (int i = 0; i < _activePlayer.Hand.Count; i++)
            {
                _activePlayer.Hand[i].Position = new Vector2(startX + (i * gap), 500);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

            var mouseState = Mouse.GetState();
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;
            bool justClicked = isClicking && !_wasMousePressed; // Only trigger once per press

            // --- UI LOGIC ---
            
            // 1. Check Toggle Button Click
            if (justClicked && _marketButtonRect.Contains(mouseState.Position))
            {
                _isMarketOpen = !_isMarketOpen; // Toggle On/Off
                _wasMousePressed = isClicking;
                return; // Stop processing other clicks this frame
            }

            if (_isMarketOpen)
            {
                // === MARKET MODE ===
                // Only update the market. The map and hand are "Frozen" in the background.
                _marketManager.Update(mouseState, _activePlayer);

                // "Click Outside" Logic
                if (justClicked)
                {
                    bool clickedOnCard = false;
                    foreach (var card in _marketManager.MarketRow)
                    {
                        if (card.IsHovered) clickedOnCard = true;
                    }

                    // If we clicked, but NOT on a card and NOT on the button... Close it.
                    if (!clickedOnCard && !_marketButtonRect.Contains(mouseState.Position))
                    {
                        _isMarketOpen = false;
                    }
                }
            }
            else
            {
                // === NORMAL GAMEPLAY MODE ===
                bool clickHandled = false;

                // Update Hand
                for (int i = _activePlayer.Hand.Count - 1; i >= 0; i--)
                {
                    var card = _activePlayer.Hand[i];
                    card.Update(gameTime, mouseState);

                    if (justClicked && card.IsHovered)
                    {
                        PlayCard(card);
                        clickHandled = true; 
                        break; 
                    }
                }

                // Update Map (only if hand wasn't clicked)
                if (!clickHandled)
                {
                    _mapManager.Update(mouseState, _activePlayer);
                }

                // Update Visuals
                foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, mouseState);
            }

            // Debug Title
            Window.Title = $"ChaosWarlords | Power: {_activePlayer.Power} | Influence: {_activePlayer.Influence} | Deck: {_activePlayer.Deck.Count}";

            _wasMousePressed = isClicking;
            base.Update(gameTime);
        }

        private void PlayCard(Card card)
        {
            GameLogger.Log($"Played Card: {card.Name}", LogChannel.Combat);
            _activePlayer.Hand.Remove(card);
            _activePlayer.PlayedCards.Add(card);
            card.Position = new Vector2(100 + (_activePlayer.PlayedCards.Count * 160), 300); 

            foreach(var effect in card.Effects)
            {
                if(effect.Type == EffectType.GainResource)
                {
                    if(effect.TargetResource == ResourceType.Power) _activePlayer.Power += effect.Amount;
                    if(effect.TargetResource == ResourceType.Influence) _activePlayer.Influence += effect.Amount;
                }
            }
            ArrangeHandVisuals();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue); 
            _spriteBatch.Begin();

            // 1. Draw Map (Bottom Layer)
            _mapManager.Draw(_spriteBatch);

            // 2. Draw Hand (Bottom Layer) - Drawn here so Market can cover it
            foreach (var card in _activePlayer.Hand) card.Draw(_spriteBatch, _defaultFont);

            // 3. Draw Played Cards (Middle Layer)
            foreach (var card in _activePlayer.PlayedCards) card.Draw(_spriteBatch, _defaultFont);

            // --- MARKET OVERLAY & MODAL ---
            if (_isMarketOpen)
            {
                // Dimmer: Draw a full-screen black rectangle at 70% opacity
                _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.7f);

                // Draw Market Cards on TOP of the dimmer
                _marketManager.Draw(_spriteBatch, _defaultFont);
                
                // Optional: Draw "MARKET" text title centered
                if (_defaultFont != null)
                    _spriteBatch.DrawString(_defaultFont, "MARKET (Buy Cards)", new Vector2(580, 20), Color.Gold);
            }

            // --- MARKET BUTTON ---
            _spriteBatch.Draw(_pixelTexture, _marketButtonRect, _isMarketOpen ? Color.Gray : Color.Gold);
            
            // Select the best available font (Small -> Default -> Null)
            SpriteFont btnFont = _smallFont ?? _defaultFont;

            if (btnFont != null)
            {
                string btnText = "M\nA\nR\nK\nE\nT";
                
                // Calculate size using the specific font we are using
                Vector2 textSize = btnFont.MeasureString(btnText);
                
                // Perfect Centering Math
                float textX = _marketButtonRect.X + (_marketButtonRect.Width - textSize.X) / 2;
                float textY = _marketButtonRect.Y + (_marketButtonRect.Height - textSize.Y) / 2;

                _spriteBatch.DrawString(
                    btnFont, 
                    btnText, 
                    new Vector2(textX, textY), 
                    Color.Black
                );
            }

            // 5. Draw UI Overlay (Top Bar)
            if(_defaultFont != null)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 40), Color.Black * 0.5f);
                
                _spriteBatch.DrawString(_defaultFont, $"Power: {_activePlayer.Power}", new Vector2(20, 10), Color.Orange);
                _spriteBatch.DrawString(_defaultFont, $"Influence: {_activePlayer.Influence}", new Vector2(150, 10), Color.Cyan);
                _spriteBatch.DrawString(_defaultFont, $"Deck: {_activePlayer.Deck.Count}", new Vector2(300, 10), Color.White);
                _spriteBatch.DrawString(_defaultFont, $"Discard: {_activePlayer.DiscardPile.Count}", new Vector2(400, 10), Color.Gray);
            }

            // 6. Draw Logger Console
            GameLogger.Draw(_spriteBatch, _defaultFont);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            GameLogger.Log("Session Ended. Flushing logs.", LogChannel.General);
            GameLogger.FlushToFile();
            base.UnloadContent();
        }
    }
}