using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using System.Collections.Generic;
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
        private SpriteFont _smallFont; // Clean font for buttons

        // SYSTEMS
        private MapManager _mapManager;
        private MarketManager _marketManager;

        // STATE
        private Player _activePlayer;
        private bool _wasMousePressed = false;
        private bool _wasEnterPressed = false; // For End Turn debounce

        // UI STATE
        private bool _isMarketOpen = false;
        private Rectangle _marketButtonRect;

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

            // Load Fonts safely
            try { _defaultFont = Content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }
            try { _smallFont = Content.Load<SpriteFont>("fonts/SmallFont"); } catch { }

            // 1. INIT LOGGING
            GameLogger.Initialize();

            // 2. LOAD CARD DATABASE
            string cardJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            CardDatabase.Load(cardJsonPath, _pixelTexture);

            // 3. SETUP MARKET
            _marketManager = new MarketManager();
            _marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            // 4. SETUP PLAYER
            _activePlayer = new Player(PlayerColor.Red);
            // Starter Deck: 3 Soldiers, 7 Nobles
            for (int i = 0; i < 3; i++) _activePlayer.Deck.Add(CardFactory.CreateSoldier(_pixelTexture));
            for (int i = 0; i < 7; i++) _activePlayer.Deck.Add(CardFactory.CreateNoble(_pixelTexture));

            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();

            // 5. SETUP MAP (Nodes + Sites)
            // We assume you have created map.json. If not, this block handles the fallback.
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "map.json");

            if (File.Exists(mapPath))
            {
                // Load from JSON
                var mapData = MapFactory.LoadFromFile(mapPath, _pixelTexture);
                _mapManager = new MapManager(mapData.Item1, mapData.Item2);
            }
            else
            {
                // Fallback for testing if json is missing
                var nodes = MapFactory.CreateTestMap(_pixelTexture);
                var sites = new List<Site>();

                // Create a dummy site so we can test scoring
                var testSite = new Site("Test City",
                                        ResourceType.Influence, 1,
                                        ResourceType.VictoryPoints, 2);

                testSite.AddNode(nodes[0]);
                testSite.AddNode(nodes[1]);
                sites.Add(testSite);

                _mapManager = new MapManager(nodes, sites);
            }

            _mapManager.PixelTexture = _pixelTexture;

            // 6. UI SETUP
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
            var keyboardState = Keyboard.GetState();
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;
            bool justClicked = isClicking && !_wasMousePressed;

            // --- END TURN LOGIC (Enter Key) ---
            if (keyboardState.IsKeyDown(Keys.Enter))
            {
                if (!_wasEnterPressed) EndTurn();
                _wasEnterPressed = true;
            }
            else
            {
                _wasEnterPressed = false;
            }

            // --- UI: MARKET TOGGLE ---
            if (justClicked && _marketButtonRect.Contains(mouseState.Position))
            {
                _isMarketOpen = !_isMarketOpen;
                _wasMousePressed = isClicking;
                return;
            }

            if (_isMarketOpen)
            {
                // MARKET MODE
                _marketManager.Update(mouseState, _activePlayer);

                // Click outside to close
                if (justClicked)
                {
                    bool clickedOnCard = false;
                    foreach (var card in _marketManager.MarketRow)
                    {
                        if (card.IsHovered) clickedOnCard = true;
                    }
                    if (!clickedOnCard && !_marketButtonRect.Contains(mouseState.Position))
                    {
                        _isMarketOpen = false;
                    }
                }
            }
            else
            {
                // GAMEPLAY MODE
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

                // Update Map
                if (!clickHandled)
                {
                    _mapManager.Update(mouseState, _activePlayer);
                }

                // Update Visuals
                foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, mouseState);
            }

            // Debug Title Update
            Window.Title = $"Power: {_activePlayer.Power} | Influence: {_activePlayer.Influence} | VP: {_activePlayer.VictoryPoints} | Deck: {_activePlayer.Deck.Count}";
            _wasMousePressed = isClicking;
            base.Update(gameTime);
        }

        private void PlayCard(Card card)
        {
            GameLogger.Log($"Played Card: {card.Name}", LogChannel.Combat);
            _activePlayer.Hand.Remove(card);
            _activePlayer.PlayedCards.Add(card);
            card.Position = new Vector2(100 + (_activePlayer.PlayedCards.Count * 160), 300);

            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.GainResource)
                {
                    if (effect.TargetResource == ResourceType.Power) _activePlayer.Power += effect.Amount;
                    if (effect.TargetResource == ResourceType.Influence) _activePlayer.Influence += effect.Amount;
                }
            }
            ArrangeHandVisuals();
        }

        private void EndTurn()
        {
            GameLogger.Log("--- TURN ENDED ---", LogChannel.General);

            // 1. CLEAN UP FIRST (Wipe unused resources from the previous turn)
            _activePlayer.CleanUpTurn();

            // 2. COLLECT SITE INCOME (For the NEXT turn)
            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites) 
                {
                    if (site.Owner == _activePlayer.Color)
                    {
                        // A. Always award Basic Control Bonus
                        ApplyReward(site.ControlResource, site.ControlAmount);
                        GameLogger.Log($"Site Control: {site.Name} gave +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);

                        // B. If Total Control, ADD the Bonus
                        if (site.HasTotalControl)
                        {
                            ApplyReward(site.TotalControlResource, site.TotalControlAmount);
                            GameLogger.Log($"Total Control Bonus: {site.Name} gave +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
                        }
                    }
                }
            }

            // 3. Draw new Hand
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();
            
            GameLogger.Log($"New Hand Drawn. Deck: {_activePlayer.Deck.Count}. Current VP: {_activePlayer.VictoryPoints}", LogChannel.General);
        }

        // Helper to avoid duplicate code
        private void ApplyReward(ResourceType type, int amount)
        {
            if (type == ResourceType.VictoryPoints) _activePlayer.VictoryPoints += amount;
            if (type == ResourceType.Power) _activePlayer.Power += amount;
            if (type == ResourceType.Influence) _activePlayer.Influence += amount;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue);
            _spriteBatch.Begin();

            // 1. Draw Map (Passing Font now for Site Labels)
            _mapManager.Draw(_spriteBatch, _defaultFont);

            // 2. Draw Hand
            foreach (var card in _activePlayer.Hand) card.Draw(_spriteBatch, _defaultFont);

            // 3. Draw Played Cards
            foreach (var card in _activePlayer.PlayedCards) card.Draw(_spriteBatch, _defaultFont);

            // --- MARKET OVERLAY ---
            if (_isMarketOpen)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.7f);
                _marketManager.Draw(_spriteBatch, _defaultFont);
                if (_defaultFont != null)
                    _spriteBatch.DrawString(_defaultFont, "MARKET (Buy Cards)", new Vector2(580, 20), Color.Gold);
            }

            // --- MARKET BUTTON ---
            _spriteBatch.Draw(_pixelTexture, _marketButtonRect, _isMarketOpen ? Color.Gray : Color.Gold);

            // Use SmallFont for the button text
            SpriteFont btnFont = _smallFont ?? _defaultFont;
            if (btnFont != null)
            {
                string btnText = "M\nA\nR\nK\nE\nT";
                Vector2 textSize = btnFont.MeasureString(btnText);
                float textX = _marketButtonRect.X + (_marketButtonRect.Width - textSize.X) / 2;
                float textY = _marketButtonRect.Y + (_marketButtonRect.Height - textSize.Y) / 2;
                _spriteBatch.DrawString(btnFont, btnText, new Vector2(textX, textY), Color.Black);
            }

            // --- UI OVERLAY ---
            if (_defaultFont != null)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 40), Color.Black * 0.5f);
                _spriteBatch.DrawString(_defaultFont, $"Power: {_activePlayer.Power}", new Vector2(20, 10), Color.Orange);
                _spriteBatch.DrawString(_defaultFont, $"Influence: {_activePlayer.Influence}", new Vector2(150, 10), Color.Cyan);
                _spriteBatch.DrawString(_defaultFont, $"VP: {_activePlayer.VictoryPoints}", new Vector2(300, 10), Color.Lime);
                _spriteBatch.DrawString(_defaultFont, $"Deck: {_activePlayer.Deck.Count}", new Vector2(400, 10), Color.White);
            }

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