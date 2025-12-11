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
    // 1. Define the States
    public enum GameState
    {
        Normal,
        TargetingAssassinate,
        TargetingReturn,
        TargetingSupplant,
        TargetingPlaceSpy,
        TargetingReturnSpy
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixelTexture;
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;

        private InputManager _inputManager;
        private UIManager _uiManager;
        private MapManager _mapManager;
        private MarketManager _marketManager;
        private ActionSystem _actionSystem;

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
            _inputManager = new InputManager();
            _uiManager = new UIManager(GraphicsDevice, _defaultFont, _smallFont);

            string cardJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            CardDatabase.Load(cardJsonPath, _pixelTexture);

            _marketManager = new MarketManager();
            _marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            _activePlayer = new Player(PlayerColor.Red);
            // Give starter deck
            for (int i = 0; i < 3; i++) _activePlayer.Deck.Add(CardFactory.CreateSoldier(_pixelTexture));
            for (int i = 0; i < 7; i++) _activePlayer.Deck.Add(CardFactory.CreateNoble(_pixelTexture));
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();

            // Map Setup
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "map.json");
            if (File.Exists(mapPath))
            {
                var mapData = MapFactory.LoadFromFile(mapPath, _pixelTexture);
                _mapManager = new MapManager(mapData.Item1, mapData.Item2);
            }
            else
            {
                var nodes = MapFactory.CreateTestMap(_pixelTexture);
                var sites = new List<Site>(); // empty fallback
                _mapManager = new MapManager(nodes, sites);
            }
            _mapManager.PixelTexture = _pixelTexture;
            _mapManager.CenterMap(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            _actionSystem = new ActionSystem(_activePlayer, _mapManager);

            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites)
                {
                    // Case-insensitive check for the city name
                    if (site.Name.ToLower().Contains("city of gold"))
                    {
                        site.Spies.Add(PlayerColor.Blue);
                        GameLogger.Log("TESTING: Blue Spy added to City of Gold.", LogChannel.General);
                    }
                }
            }
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
            _inputManager.Update();

            if (_inputManager.IsKeyJustPressed(Keys.Escape)) Exit();

            // Cancel Target
            if (_inputManager.IsRightMouseJustClicked() && _actionSystem.IsTargeting())
            {
                _actionSystem.CancelTargeting();
            }

            if (_inputManager.IsKeyJustPressed(Keys.Enter)) EndTurn();

            // --- UI BUTTON CLICKS ---
            if (_inputManager.IsLeftMouseJustClicked())
            {
                // 1. Market Toggle
                if (_uiManager.IsMarketButtonHovered(_inputManager))
                {
                    _isMarketOpen = !_isMarketOpen;
                    return;
                }

                // Only check Action buttons if Market is closed and Normal state
                if (!_isMarketOpen && !_actionSystem.IsTargeting())
                {
                    // 2. Assassinate Button
                    if (_uiManager.IsAssassinateButtonHovered(_inputManager))
                    {
                        // The ActionSystem now handles the cost check.
                        _actionSystem.TryStartAssassinate();
                        return;
                    }

                    // 3. Return Spy Button <--- NEW
                    if (_uiManager.IsReturnSpyButtonHovered(_inputManager))
                    {
                        // The ActionSystem now handles the cost check.
                        _actionSystem.TryStartReturnSpy();
                        return;
                    }
                }
            }

            // --- GAME LOOP ---
            if (_isMarketOpen)
            {
                UpdateMarketLogic();
            }
            else
            {
                if (!_actionSystem.IsTargeting())
                    UpdateNormalGameplay(gameTime);
                else
                    UpdateTargetingLogic(); // <--- This method is fixed below
            }

            base.Update(gameTime);
        }

        private void UpdateMarketLogic()
        {
            _marketManager.Update(_inputManager.GetMouseState(), _activePlayer);
            if (_inputManager.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                foreach (var card in _marketManager.MarketRow) { if (card.IsHovered) clickedOnCard = true; }
                if (!clickedOnCard && !_uiManager.IsMarketButtonHovered(_inputManager)) _isMarketOpen = false;
            }
        }

        private void UpdateNormalGameplay(GameTime gameTime)
        {
            bool clickHandled = false;

            // 1. Play Cards (Keep existing logic)
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

            // 2. Deploy Logic (UPDATED)
            if (!clickHandled)
            {
                // A. Visuals: Always update hover effects
                _mapManager.Update(_inputManager.GetMouseState());

                // B. Logic: Only try to deploy if we clicked!
                if (_inputManager.IsLeftMouseJustClicked())
                {
                    _mapManager.TryDeploy(_activePlayer);
                }
            }

            foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, _inputManager.GetMouseState());
        }

        private void UpdateTargetingLogic()
        {
            _mapManager.Update(_inputManager.GetMouseState());

            if (!_inputManager.IsLeftMouseJustClicked()) return;

            MapNode targetNode = _mapManager.GetHoveredNode();
            Site targetSite = _mapManager.GetHoveredSite(_inputManager.MousePosition);

            // If a node is clicked inside a site, the target is the site.
            // This handles cases where site-targeting actions are intended but a node is clicked.
            if (targetNode != null && targetSite == null)
            {
                // This is a potential future improvement: targetSite = _mapManager.GetSiteForNode(targetNode);
            }

            bool success = _actionSystem.HandleTargetClick(targetNode, targetSite);

            if (success)
            {
                // Action was successful, now finalize it.
                if (_actionSystem.PendingCard != null) // Action came from a card.
                {
                    ResolveCardEffects(_actionSystem.PendingCard);
                    MoveCardToPlayed(_actionSystem.PendingCard);
                }
                // If it was a UI action, the cost was already paid inside the ActionSystem.

                // Reset state machine
                _actionSystem.CancelTargeting();
                GameLogger.Log("Action Complete.", LogChannel.General);
            }
            // If not successful, the ActionSystem has already logged the error.
        }

        private void PlayCard(Card card)
        {
            // 1. Check for Interactive Effects
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.Assassinate)
                {
                    _actionSystem.StartTargeting(GameState.TargetingAssassinate, card);
                    GameLogger.Log("Select a target to Assassinate... (Right Click to Cancel)", LogChannel.General);
                    return;
                }
                else if (effect.Type == EffectType.ReturnUnit)
                {
                    _actionSystem.StartTargeting(GameState.TargetingReturn, card);
                    GameLogger.Log("Select a unit to Return... (Right Click to Cancel)", LogChannel.General);
                    return;
                }
                else if (effect.Type == EffectType.Supplant)
                {
                    _actionSystem.StartTargeting(GameState.TargetingSupplant, card);
                    GameLogger.Log("Select a target to Supplant... (Right Click to Cancel)", LogChannel.General);
                    return;
                }
                else if (effect.Type == EffectType.PlaceSpy)
                {
                    _actionSystem.StartTargeting(GameState.TargetingPlaceSpy, card);
                    GameLogger.Log("Select a Site to Place a Spy... (Right Click to Cancel)", LogChannel.General);
                    return;
                }
            }

            // 2. If no interactions, resolve immediately
            ResolveCardEffects(card);
            MoveCardToPlayed(card);
        }

        private void ResolveCardEffects(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.GainResource)
                {
                    if (effect.TargetResource == ResourceType.Power) _activePlayer.Power += effect.Amount;
                    if (effect.TargetResource == ResourceType.Influence) _activePlayer.Influence += effect.Amount;
                }
            }
        }

        private void MoveCardToPlayed(Card card)
        {
            GameLogger.Log($"Played Card: {card.Name}", LogChannel.Combat);
            _activePlayer.Hand.Remove(card);
            _activePlayer.PlayedCards.Add(card);
            card.Position = new Vector2(100 + (_activePlayer.PlayedCards.Count * 160), 300);
            ArrangeHandVisuals();
        }

        // --- RESTORED ENDTURN METHOD ---
        private void EndTurn()
        {
            if (_actionSystem.IsTargeting())
            {
                _actionSystem.CancelTargeting();
            }

            GameLogger.Log("--- TURN ENDED ---", LogChannel.General);

            // 1. Clean up old resources
            _activePlayer.CleanUpTurn();

            // 2. Collect Income for new turn (ONLY for Cities!)
            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites)
                {
                    if (site.Owner == _activePlayer.Color && site.IsCity)
                    {
                        _mapManager.ApplyReward(_activePlayer, site.ControlResource, site.ControlAmount);
                        GameLogger.Log($"Site Control: {site.Name} gave +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);

                        if (site.HasTotalControl)
                        {
                            _mapManager.ApplyReward(_activePlayer, site.TotalControlResource, site.TotalControlAmount);
                            GameLogger.Log($"Total Control Bonus: {site.Name} gave +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
                        }
                    }
                }
            }

            // 3. Draw new Hand
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();

            GameLogger.Log($"New Hand Drawn. Total VP: {_activePlayer.VictoryPoints}", LogChannel.General);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue);
            _spriteBatch.Begin();

            _mapManager.Draw(_spriteBatch, _defaultFont);

            foreach (var card in _activePlayer.Hand) card.Draw(_spriteBatch, _defaultFont);
            foreach (var card in _activePlayer.PlayedCards) card.Draw(_spriteBatch, _defaultFont);

            if (_isMarketOpen)
            {
                _uiManager.DrawMarketOverlay(_spriteBatch);
                _marketManager.Draw(_spriteBatch, _defaultFont);
            }

            _uiManager.DrawMarketButton(_spriteBatch, _isMarketOpen);

            // UPDATED: Draw the Action Buttons
            _uiManager.DrawActionButtons(_spriteBatch, _activePlayer);

            _uiManager.DrawTopBar(_spriteBatch, _activePlayer);

            // Debug Text for State
            if (_actionSystem.IsTargeting() && _defaultFont != null)
            {
                var currentState = _actionSystem.CurrentState;
                string targetText = "TARGETING...";
                if (currentState == GameState.TargetingAssassinate) targetText = "CLICK TROOP TO KILL";
                if (currentState == GameState.TargetingPlaceSpy) targetText = "CLICK SITE TO PLACE SPY";
                if (currentState == GameState.TargetingReturnSpy) targetText = "CLICK SITE TO HUNT SPY";

                Vector2 mousePos = _inputManager.MousePosition;
                _spriteBatch.DrawString(_defaultFont, targetText, mousePos + new Vector2(20, 20), Color.Red);
            }

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