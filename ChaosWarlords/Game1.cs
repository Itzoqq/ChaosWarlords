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
        private SpriteFont _smallFont;

        // SYSTEMS
        private InputManager _inputManager;
        private UIManager _uiManager;
        private MapManager _mapManager;
        private MarketManager _marketManager;

        // STATE
        private Player _activePlayer;
        private bool _isMarketOpen = false;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.IsFullScreen = true;
            _graphics.HardwareModeSwitch = false;
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

            // 1. INIT SYSTEMS
            _inputManager = new InputManager();
            _uiManager = new UIManager(GraphicsDevice, _defaultFont, _smallFont);

            // 2. LOAD DATA
            string cardJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            CardDatabase.Load(cardJsonPath, _pixelTexture);

            _marketManager = new MarketManager();
            _marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            // 3. SETUP PLAYER
            _activePlayer = new Player(PlayerColor.Red);
            for (int i = 0; i < 3; i++) _activePlayer.Deck.Add(CardFactory.CreateSoldier(_pixelTexture));
            for (int i = 0; i < 7; i++) _activePlayer.Deck.Add(CardFactory.CreateNoble(_pixelTexture));
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();

            // 4. SETUP MAP
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "map.json");
            if (File.Exists(mapPath))
            {
                var mapData = MapFactory.LoadFromFile(mapPath, _pixelTexture);
                _mapManager = new MapManager(mapData.Item1, mapData.Item2);
            }
            else
            {
                var nodes = MapFactory.CreateTestMap(_pixelTexture);
                var sites = new List<Site>();
                var testSite = new Site("Test City", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
                testSite.AddNode(nodes[0]);
                testSite.AddNode(nodes[1]);
                sites.Add(testSite);
                _mapManager = new MapManager(nodes, sites);
            }

            _mapManager.PixelTexture = _pixelTexture;
            _mapManager.CenterMap(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        private void ArrangeHandVisuals()
        {
            int cardWidth = 150;
            int gap = 10;
            int totalHandWidth = (_activePlayer.Hand.Count * cardWidth) + ((_activePlayer.Hand.Count - 1) * gap);
            int startX = (_graphics.GraphicsDevice.Viewport.Width - totalHandWidth) / 2;
            int startY = _graphics.GraphicsDevice.Viewport.Height - 200 - 20;

            for (int i = 0; i < _activePlayer.Hand.Count; i++)
            {
                _activePlayer.Hand[i].Position = new Vector2(startX + (i * (cardWidth + gap)), startY);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            // 1. UPDATE INPUT
            _inputManager.Update();

            if (_inputManager.IsKeyJustPressed(Keys.Escape)) Exit();

            // 2. END TURN (Enter)
            if (_inputManager.IsKeyJustPressed(Keys.Enter))
            {
                EndTurn();
            }

            // 3. UI TOGGLES
            if (_inputManager.IsLeftMouseJustClicked() && _uiManager.IsMarketButtonHovered(_inputManager))
            {
                _isMarketOpen = !_isMarketOpen;
                return; // Stop processing this click
            }

            // 4. GAMEPLAY LOGIC
            if (_isMarketOpen)
            {
                UpdateMarketLogic();
            }
            else
            {
                UpdateGameplayLogic(gameTime);
            }

            base.Update(gameTime);
        }

        private void UpdateMarketLogic()
        {
            // Only update the market logic (hovering, buying)
            // Note: We pass the raw mouse state for compatibility with existing managers for now, 
            // but eventually managers should use InputManager too.
            _marketManager.Update(_inputManager.GetMouseState(), _activePlayer);

            // Close if clicked outside
            if (_inputManager.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                foreach (var card in _marketManager.MarketRow)
                {
                    if (card.IsHovered) clickedOnCard = true;
                }

                // If clicked void and NOT button (handled in main update)
                if (!clickedOnCard && !_uiManager.IsMarketButtonHovered(_inputManager))
                {
                    _isMarketOpen = false;
                }
            }
        }

        private void UpdateGameplayLogic(GameTime gameTime)
        {
            bool clickHandled = false;

            // A. Update Hand (Play Cards)
            for (int i = _activePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = _activePlayer.Hand[i];
                card.Update(gameTime, _inputManager.GetMouseState());

                if (_inputManager.IsLeftMouseJustClicked() && card.IsHovered)
                {
                    PlayCard(card);
                    clickHandled = true;
                    break;
                }
            }

            // B. Update Map (Deploy)
            if (!clickHandled)
            {
                _mapManager.Update(_inputManager.GetMouseState(), _activePlayer);
            }

            // C. Visual Updates
            foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, _inputManager.GetMouseState());
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

            // Clean up old resources first
            _activePlayer.CleanUpTurn();

            // Collect Income for new turn
            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites)
                {
                    if (site.Owner == _activePlayer.Color)
                    {
                        ApplyReward(site.ControlResource, site.ControlAmount);
                        GameLogger.Log($"Site Control: {site.Name} gave +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);

                        if (site.HasTotalControl)
                        {
                            ApplyReward(site.TotalControlResource, site.TotalControlAmount);
                            GameLogger.Log($"Total Control Bonus: {site.Name} gave +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
                        }
                    }
                }
            }

            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();

            GameLogger.Log($"New Hand Drawn. Total VP: {_activePlayer.VictoryPoints}", LogChannel.General);
        }

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

            // 1. Draw World
            _mapManager.Draw(_spriteBatch, _defaultFont);

            // Draw Hand (Behind market)
            foreach (var card in _activePlayer.Hand) card.Draw(_spriteBatch, _defaultFont);
            foreach (var card in _activePlayer.PlayedCards) card.Draw(_spriteBatch, _defaultFont);

            // 2. Draw Market Overlay
            if (_isMarketOpen)
            {
                _uiManager.DrawMarketOverlay(_spriteBatch);
                _marketManager.Draw(_spriteBatch, _defaultFont);
            }

            // 3. Draw UI Chrome
            _uiManager.DrawMarketButton(_spriteBatch, _isMarketOpen);
            _uiManager.DrawTopBar(_spriteBatch, _activePlayer);

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