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
        TargetingSupplant
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

        private Player _activePlayer;
        private bool _isMarketOpen = false;

        // --- STATE MACHINE VARIABLES ---
        private GameState _currentState = GameState.Normal;
        private Card _pendingCard; // The card waiting to be resolved

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
            if (_inputManager.IsRightMouseJustClicked() && _currentState != GameState.Normal)
            {
                _currentState = GameState.Normal;
                _pendingCard = null;
                GameLogger.Log("Targeting Cancelled.", LogChannel.General);
            }

            if (_inputManager.IsKeyJustPressed(Keys.Enter)) EndTurn();

            // 1. Market Toggle
            if (_inputManager.IsLeftMouseJustClicked() && _uiManager.IsMarketButtonHovered(_inputManager))
            {
                _isMarketOpen = !_isMarketOpen;
                return;
            }

            // 2. Assassinate Button (Global Action) -- NEW
            if (_inputManager.IsLeftMouseJustClicked() && _uiManager.IsAssassinateButtonHovered(_inputManager))
            {
                if (_currentState == GameState.Normal && !_isMarketOpen)
                {
                    if (_activePlayer.Power >= 3)
                    {
                        _currentState = GameState.TargetingAssassinate;
                        _pendingCard = null; // No card involved, pure power!
                        GameLogger.Log("Select a target to Assassinate (Cost: 3 Power)...", LogChannel.General);
                    }
                    else
                    {
                        GameLogger.Log("Not enough Power! Need 3.", LogChannel.Economy);
                    }
                }
                return;
            }

            if (_isMarketOpen)
            {
                UpdateMarketLogic();
            }
            else
            {
                if (_currentState == GameState.Normal)
                    UpdateNormalGameplay(gameTime);
                else
                    UpdateTargetingLogic();
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

            // 1. Play Cards
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

            // 2. Deploy Logic
            if (!clickHandled)
            {
                _mapManager.Update(_inputManager.GetMouseState(), _activePlayer);
            }

            foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, _inputManager.GetMouseState());
        }

        private void UpdateTargetingLogic()
        {
            _mapManager.Update(_inputManager.GetMouseState(), _activePlayer);

            if (_inputManager.IsLeftMouseJustClicked())
            {
                MapNode target = _mapManager.GetHoveredNode();
                if (target != null)
                {
                    bool success = false;

                    if (_currentState == GameState.TargetingAssassinate)
                    {
                        // Rule: Enemy + Presence
                        if (target.Occupant != PlayerColor.None &&
                            target.Occupant != _activePlayer.Color &&
                            _mapManager.HasPresence(target, _activePlayer.Color))
                        {
                            _mapManager.Assassinate(target, _activePlayer);
                            success = true;
                        }
                    }
                    else if (_currentState == GameState.TargetingReturn)
                    {
                        // Rule: Occupied + Presence + Not Neutral
                        if (target.Occupant != PlayerColor.None &&
                            _mapManager.HasPresence(target, _activePlayer.Color))
                        {
                            if (target.Occupant == PlayerColor.Neutral)
                            {
                                GameLogger.Log("Invalid Target: Cannot Return Neutral troops!", LogChannel.Error);
                            }
                            else
                            {
                                _mapManager.ReturnTroop(target, _activePlayer);
                                success = true;
                            }
                        }
                    }
                    else if (_currentState == GameState.TargetingSupplant)
                    {
                        // Rule: Enemy + Presence + Supply
                        if (target.Occupant != PlayerColor.None &&
                            target.Occupant != _activePlayer.Color &&
                            _mapManager.HasPresence(target, _activePlayer.Color))
                        {
                            if (_activePlayer.TroopsInBarracks > 0)
                            {
                                _mapManager.Supplant(target, _activePlayer);
                                success = true;
                            }
                            else
                            {
                                GameLogger.Log("Cannot Supplant: Barracks Empty!", LogChannel.Error);
                            }
                        }
                    }

                    if (success)
                    {
                        // Handle CARD Usage
                        if (_pendingCard != null)
                        {
                            ResolveCardEffects(_pendingCard);
                            MoveCardToPlayed(_pendingCard);
                        }
                        // Handle POWER Usage (Global Action)
                        else if (_currentState == GameState.TargetingAssassinate)
                        {
                            _activePlayer.Power -= 3;
                            GameLogger.Log("Expended 3 Power.", LogChannel.Economy);
                        }

                        _currentState = GameState.Normal;
                        _pendingCard = null;
                        GameLogger.Log("Targeting Complete.", LogChannel.General);
                    }
                    else
                    {
                        // If logic failed (e.g. no presence, neutral target), 
                        // we intentionally DO NOT reset state, allowing the player to try again.
                        if (target.Occupant == PlayerColor.None)
                            GameLogger.Log("Invalid Target: Empty Node.", LogChannel.Error);
                        else if (!success && target.Occupant == PlayerColor.Neutral && _currentState == GameState.TargetingReturn)
                        { /* Already logged above */ }
                        else if (!success)
                            GameLogger.Log("Invalid Target! (Check Presence rules)", LogChannel.Error);
                    }
                }
            }
        }

        private void PlayCard(Card card)
        {
            // 1. Check for Interactive Effects
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.Assassinate)
                {
                    _currentState = GameState.TargetingAssassinate;
                    _pendingCard = card;
                    GameLogger.Log("Select a target to Assassinate... (Right Click to Cancel)", LogChannel.General);
                    return;
                }
                else if (effect.Type == EffectType.ReturnUnit)
                {
                    _currentState = GameState.TargetingReturn;
                    _pendingCard = card;
                    GameLogger.Log("Select a unit to Return... (Right Click to Cancel)", LogChannel.General);
                    return;
                }
                else if (effect.Type == EffectType.Supplant)
                {
                    _currentState = GameState.TargetingSupplant;
                    _pendingCard = card;
                    GameLogger.Log("Select a target to Supplant... (Right Click to Cancel)", LogChannel.General);
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
            // Safety: Reset Targeting if user hits Enter mid-action
            if (_currentState != GameState.Normal)
            {
                _currentState = GameState.Normal;
                _pendingCard = null;
                GameLogger.Log("Targeting Cancelled due to End Turn.", LogChannel.General);
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

            // 3. Draw new Hand
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
            _uiManager.DrawAssassinateButton(_spriteBatch, _activePlayer);
            _uiManager.DrawTopBar(_spriteBatch, _activePlayer);

            // 4. Draw Targeting Cursor (Optional, helps player know state)
            if (_currentState != GameState.Normal && _defaultFont != null)
            {
                string targetText = "SELECT TARGET";
                if (_currentState == GameState.TargetingAssassinate) targetText = "ASSASSINATE TARGET";
                if (_currentState == GameState.TargetingReturn) targetText = "RETURN TROOP";
                if (_currentState == GameState.TargetingSupplant) targetText = "SUPPLANT TARGET";

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