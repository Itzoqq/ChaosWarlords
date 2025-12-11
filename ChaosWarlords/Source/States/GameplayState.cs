using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using System.Collections.Generic;
using System.IO;
using System;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IState
    {
        private readonly Game _game;
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;

        private InputManager _inputManager;
        private UIManager _uiManager;
        private MapManager _mapManager;
        private MarketManager _marketManager;
        private ActionSystem _actionSystem;

        private Player _activePlayer;
        private bool _isMarketOpen = false;

        public GameplayState(Game game)
        {
            _game = game;
        }

        public void LoadContent()
        {
            var graphicsDevice = _game.GraphicsDevice;
            var content = _game.Content;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }
            try { _smallFont = content.Load<SpriteFont>("fonts/SmallFont"); } catch { }

            GameLogger.Initialize();
            _inputManager = new InputManager();
            _uiManager = new UIManager(graphicsDevice, _defaultFont, _smallFont);

            string cardJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            CardDatabase.Load(cardJsonPath, _pixelTexture);

            _marketManager = new MarketManager();
            _marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            _activePlayer = new Player(PlayerColor.Red);
            // Starter Deck Setup
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
                var sites = new List<Site>();
                _mapManager = new MapManager(nodes, sites);
            }
            _mapManager.PixelTexture = _pixelTexture;
            _mapManager.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            _actionSystem = new ActionSystem(_activePlayer, _mapManager);

            // Test Setup (City of Gold spy)
            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites)
                {
                    if (site.Name.ToLower().Contains("city of gold"))
                    {
                        site.Spies.Add(PlayerColor.Blue);
                    }
                }
            }
        }

        public void UnloadContent()
        {
            // Cleanup if needed
        }

        public void Update(GameTime gameTime)
        {
            _inputManager.Update();

            // 1. Handle Global Inputs (Exit, End Turn, UI Toggles)
            if (HandleGlobalInput()) return;

            // 2. Delegate to specific state logic
            if (_isMarketOpen)
            {
                UpdateMarketLogic();
            }
            else if (_actionSystem.IsTargeting())
            {
                UpdateTargetingLogic();
            }
            else
            {
                UpdateNormalGameplay(gameTime);
            }
        }

        private bool HandleGlobalInput()
        {
            if (_inputManager.IsKeyJustPressed(Keys.Escape))
            {
                _game.Exit();
                return true;
            }

            if (_inputManager.IsKeyJustPressed(Keys.Enter))
            {
                EndTurn();
                return true;
            }

            // Right Click Cancel
            if (_inputManager.IsRightMouseJustClicked() && _actionSystem.IsTargeting())
            {
                _actionSystem.CancelTargeting();
                return true;
            }

            // UI Click Handling
            if (_inputManager.IsLeftMouseJustClicked())
            {
                if (_uiManager.IsMarketButtonHovered(_inputManager))
                {
                    _isMarketOpen = !_isMarketOpen;
                    return true; // Input consumed
                }

                // Only check action buttons if we aren't busy elsewhere
                if (!_isMarketOpen && !_actionSystem.IsTargeting())
                {
                    if (CheckActionButtons()) return true;
                }
            }

            return false;
        }

        private bool CheckActionButtons()
        {
            if (_uiManager.IsAssassinateButtonHovered(_inputManager))
            {
                _actionSystem.TryStartAssassinate();
                return true;
            }
            if (_uiManager.IsReturnSpyButtonHovered(_inputManager))
            {
                _actionSystem.TryStartReturnSpy();
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            _mapManager.Draw(spriteBatch, _defaultFont);

            foreach (var card in _activePlayer.Hand) card.Draw(spriteBatch, _defaultFont);
            foreach (var card in _activePlayer.PlayedCards) card.Draw(spriteBatch, _defaultFont);

            if (_isMarketOpen)
            {
                _uiManager.DrawMarketOverlay(spriteBatch);
                _marketManager.Draw(spriteBatch, _defaultFont);
            }

            _uiManager.DrawMarketButton(spriteBatch, _isMarketOpen);
            _uiManager.DrawActionButtons(spriteBatch, _activePlayer);
            _uiManager.DrawTopBar(spriteBatch, _activePlayer);

            // Debug/Targeting Text
            if (_actionSystem.IsTargeting() && _defaultFont != null)
            {
                var currentState = _actionSystem.CurrentState;
                string targetText = "TARGETING...";
                if (currentState == ActionState.TargetingAssassinate) targetText = "CLICK TROOP TO KILL";
                if (currentState == ActionState.TargetingPlaceSpy) targetText = "CLICK SITE TO PLACE SPY";
                if (currentState == ActionState.TargetingReturnSpy) targetText = "CLICK SITE TO HUNT SPY";

                Vector2 mousePos = _inputManager.MousePosition;
                spriteBatch.DrawString(_defaultFont, targetText, mousePos + new Vector2(20, 20), Color.Red);
            }
        }

        // --- Helper Methods (Copied from previous Game1) ---

        private void ArrangeHandVisuals()
        {
            int cardWidth = 150;
            int gap = 10;
            int totalHandWidth = (_activePlayer.Hand.Count * cardWidth) + ((_activePlayer.Hand.Count - 1) * gap);
            int startX = (_game.GraphicsDevice.Viewport.Width - totalHandWidth) / 2;
            int startY = _game.GraphicsDevice.Viewport.Height - 200 - 20;

            for (int i = 0; i < _activePlayer.Hand.Count; i++)
            {
                _activePlayer.Hand[i].Position = new Vector2(startX + (i * (cardWidth + gap)), startY);
            }
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

            // Play Cards
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

            // Deploy Logic
            if (!clickHandled)
            {
                _mapManager.Update(_inputManager.GetMouseState());
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

            bool success = _actionSystem.HandleTargetClick(targetNode, targetSite);

            if (success)
            {
                if (_actionSystem.PendingCard != null)
                {
                    ResolveCardEffects(_actionSystem.PendingCard);
                    MoveCardToPlayed(_actionSystem.PendingCard);
                }
                _actionSystem.CancelTargeting();
                GameLogger.Log("Action Complete.", LogChannel.General);
            }
        }

        private void PlayCard(Card card)
        {
            // Interactive Effects
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.Assassinate)
                {
                    _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);
                    return;
                }
                else if (effect.Type == EffectType.ReturnUnit)
                {
                    _actionSystem.StartTargeting(ActionState.TargetingReturn, card);
                    return;
                }
                else if (effect.Type == EffectType.Supplant)
                {
                    _actionSystem.StartTargeting(ActionState.TargetingSupplant, card);
                    return;
                }
                else if (effect.Type == EffectType.PlaceSpy)
                {
                    _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy, card);
                    return;
                }
            }

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
            _activePlayer.Hand.Remove(card);
            _activePlayer.PlayedCards.Add(card);
            card.Position = new Vector2(100 + (_activePlayer.PlayedCards.Count * 160), 300);
            ArrangeHandVisuals();
        }

        private void EndTurn()
        {
            if (_actionSystem.IsTargeting()) _actionSystem.CancelTargeting();

            GameLogger.Log("--- TURN ENDED ---", LogChannel.General);
            _activePlayer.CleanUpTurn();

            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites)
                {
                    if (site.Owner == _activePlayer.Color && site.IsCity)
                    {
                        _mapManager.ApplyReward(_activePlayer, site.ControlResource, site.ControlAmount);
                        if (site.HasTotalControl)
                        {
                            _mapManager.ApplyReward(_activePlayer, site.TotalControlResource, site.TotalControlAmount);
                        }
                    }
                }
            }

            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();
        }
    }
}