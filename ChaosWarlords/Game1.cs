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
            if (_inputManager.IsRightMouseJustClicked() && _currentState != GameState.Normal)
            {
                _currentState = GameState.Normal;
                _pendingCard = null;
                GameLogger.Log("Targeting Cancelled.", LogChannel.General);
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
                if (!_isMarketOpen && _currentState == GameState.Normal)
                {
                    // 2. Assassinate Button
                    if (_uiManager.IsAssassinateButtonHovered(_inputManager))
                    {
                        if (_activePlayer.Power >= 3)
                        {
                            _currentState = GameState.TargetingAssassinate;
                            _pendingCard = null;
                            GameLogger.Log("Select a TROOP to Assassinate (Cost: 3 Power)...", LogChannel.General);
                        }
                        else GameLogger.Log("Not enough Power! Need 3.", LogChannel.Economy);
                        return;
                    }

                    // 3. Return Spy Button <--- NEW
                    if (_uiManager.IsReturnSpyButtonHovered(_inputManager))
                    {
                        if (_activePlayer.Power >= 3)
                        {
                            _currentState = GameState.TargetingReturnSpy;
                            _pendingCard = null;
                            GameLogger.Log("Select a SITE to remove Enemy Spy (Cost: 3 Power)...", LogChannel.General);
                        }
                        else GameLogger.Log("Not enough Power! Need 3.", LogChannel.Economy);
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
                if (_currentState == GameState.Normal)
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
                MapNode targetNode = _mapManager.GetHoveredNode();
                Site targetSite = _mapManager.GetHoveredSite(_inputManager.MousePosition);
                bool success = false;

                // --- LOGIC SPLIT BY STATE, NOT BY HIT ---

                // GROUP A: Actions that target NODES (Troops)
                if (_currentState == GameState.TargetingAssassinate ||
                    _currentState == GameState.TargetingReturn ||
                    _currentState == GameState.TargetingSupplant)
                {
                    if (targetNode != null)
                    {
                        if (_currentState == GameState.TargetingAssassinate)
                        {
                            if (_mapManager.CanAssassinate(targetNode, _activePlayer))
                            {
                                _mapManager.Assassinate(targetNode, _activePlayer);
                                success = true;
                            }
                            else GameLogger.Log("Invalid Target! Need Presence or cannot target self/empty.", LogChannel.Error);
                        }
                        else if (_currentState == GameState.TargetingReturn)
                        {
                            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, _activePlayer.Color))
                            {
                                if (targetNode.Occupant == PlayerColor.Neutral) GameLogger.Log("Cannot return Neutral troops.", LogChannel.Error);
                                else
                                {
                                    _mapManager.ReturnTroop(targetNode, _activePlayer);
                                    success = true;
                                }
                            }
                            else GameLogger.Log("Invalid Return Target.", LogChannel.Error);
                        }
                        else if (_currentState == GameState.TargetingSupplant)
                        {
                            if (_mapManager.CanAssassinate(targetNode, _activePlayer))
                            {
                                if (_activePlayer.TroopsInBarracks > 0)
                                {
                                    _mapManager.Supplant(targetNode, _activePlayer);
                                    success = true;
                                }
                                else GameLogger.Log("Barracks Empty!", LogChannel.Error);
                            }
                        }
                    }
                }
                // GROUP B: Actions that target SITES (Spies)
                else if (_currentState == GameState.TargetingPlaceSpy ||
                         _currentState == GameState.TargetingReturnSpy)
                {
                    // FIX: If we clicked a Node, we likely meant the Site underneath it.
                    // If targetSite is null but targetNode is NOT, check if the node belongs to a site.
                    // (But usually GetHoveredSite works on bounds, so targetSite should ALREADY be valid 
                    // even if clicking a node, as long as we don't 'else' it away!)

                    if (targetSite != null)
                    {
                        if (_currentState == GameState.TargetingPlaceSpy)
                        {
                            if (targetSite.Spies.Contains(_activePlayer.Color))
                            {
                                GameLogger.Log("You already have a spy here.", LogChannel.Error);
                            }
                            else if (_activePlayer.SpiesInBarracks > 0)
                            {
                                _mapManager.PlaceSpy(targetSite, _activePlayer);
                                success = true;
                            }
                            else GameLogger.Log("No Spies in Barracks!", LogChannel.Error);
                        }
                        else if (_currentState == GameState.TargetingReturnSpy)
                        {
                            if (_mapManager.ReturnSpy(targetSite, _activePlayer))
                            {
                                success = true;
                            }
                        }
                    }
                }

                // --- FINALIZE ---
                if (success)
                {
                    if (_pendingCard == null)
                    {
                        if (_currentState == GameState.TargetingAssassinate || _currentState == GameState.TargetingReturnSpy)
                        {
                            _activePlayer.Power -= 3;
                            GameLogger.Log("Power deducted: 3", LogChannel.Economy);
                        }
                    }
                    else
                    {
                        ResolveCardEffects(_pendingCard);
                        MoveCardToPlayed(_pendingCard);
                    }

                    _currentState = GameState.Normal;
                    _pendingCard = null;
                    GameLogger.Log("Action Complete.", LogChannel.General);
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
                else if (effect.Type == EffectType.PlaceSpy)
                {
                    _currentState = GameState.TargetingPlaceSpy;
                    _pendingCard = card;
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
            if (_currentState != GameState.Normal && _defaultFont != null)
            {
                string targetText = "TARGETING...";
                if (_currentState == GameState.TargetingAssassinate) targetText = "CLICK TROOP TO KILL";
                if (_currentState == GameState.TargetingPlaceSpy) targetText = "CLICK SITE TO PLACE SPY";
                if (_currentState == GameState.TargetingReturnSpy) targetText = "CLICK SITE TO HUNT SPY";

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