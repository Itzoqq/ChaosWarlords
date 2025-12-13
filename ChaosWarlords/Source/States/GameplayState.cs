using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views; // Added Reference
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

        internal InputManager _inputManager;
        internal UIManager _uiManager;
        internal MapManager _mapManager;
        internal MarketManager _marketManager;
        internal ActionSystem _actionSystem;
        internal Player _activePlayer;
        internal bool _isMarketOpen = false;

        // View
        private MapRenderer _mapRenderer;

        public GameplayState(Game game)
        {
            _game = game;
        }

        // Updated InjectDependencies for testing (removed unused ui, added player)
        internal void InjectDependencies(
            InputManager input,
            UIManager ui,
            MapManager map,
            MarketManager market,
            ActionSystem action,
            Player player)
        {
            _inputManager = input;
            _uiManager = ui;
            _mapManager = map;
            _marketManager = market;
            _actionSystem = action;
            _activePlayer = player;
        }

        public void LoadContent()
        {
            if (_game == null) return;

            var graphicsDevice = _game.GraphicsDevice;
            var content = _game.Content;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }
            try { _smallFont = content.Load<SpriteFont>("fonts/SmallFont"); } catch { }

            GameLogger.Initialize();

            var inputProvider = new MonoGameInputProvider();
            _inputManager = new InputManager(inputProvider);
            _uiManager = new UIManager(graphicsDevice, _defaultFont, _smallFont);

            // Create Renderer
            _mapRenderer = new MapRenderer(_pixelTexture, _pixelTexture, _defaultFont);

            // Builder no longer needs texture for map
            string cardPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "map.json");

            var builder = new WorldBuilder(cardPath, mapPath);
            var worldData = builder.Build(_pixelTexture); // Texture still needed for Cards currently

            _activePlayer = worldData.Player;
            _marketManager = worldData.MarketManager;
            _mapManager = worldData.MapManager;
            _actionSystem = worldData.ActionSystem;

            ArrangeHandVisuals();
            _mapManager.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
        }

        public void UnloadContent() { }

        public void Update(GameTime gameTime)
        {
            if (_inputManager == null) return;
            _inputManager.Update();
            if (HandleGlobalInput()) return;

            if (_isMarketOpen) UpdateMarketLogic();
            else if (_actionSystem.IsTargeting()) UpdateTargetingLogic();
            else UpdateNormalGameplay(gameTime);
        }

        internal bool HandleGlobalInput()
        {
            if (_inputManager.IsKeyJustPressed(Keys.Escape)) { if (_game != null) _game.Exit(); return true; }
            if (_inputManager.IsKeyJustPressed(Keys.Enter)) { EndTurn(); return true; }
            return false;
        }

        internal void UpdateNormalGameplay(GameTime gameTime)
        {
            bool clickHandled = false;
            // Card logic (Still hybrid for now)
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

            if (!clickHandled)
            {
                if (_inputManager.IsLeftMouseJustClicked())
                {
                    // Map interaction via GetNodeAt
                    var clickedNode = _mapManager.GetNodeAt(_inputManager.MousePosition);
                    if (clickedNode != null)
                    {
                        _mapManager.TryDeploy(_activePlayer, clickedNode);
                    }
                }
            }
            foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, _inputManager.GetMouseState());
        }

        internal void UpdateTargetingLogic()
        {
            if (!_inputManager.IsLeftMouseJustClicked()) return;

            Vector2 mousePos = _inputManager.MousePosition;
            MapNode targetNode = _mapManager.GetNodeAt(mousePos);
            Site targetSite = _mapManager.GetSiteAt(mousePos);

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

        // ... (UpdateMarketLogic, PlayCard, EndTurn etc. same as before, simplified slightly) ...
        internal void UpdateMarketLogic()
        {
            _marketManager.Update(_inputManager.GetMouseState(), _activePlayer);
            if (_inputManager.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                foreach (var card in _marketManager.MarketRow) { if (card.IsHovered) clickedOnCard = true; }
                bool clickedButton = _uiManager != null && _uiManager.IsMarketButtonHovered(_inputManager);
                if (!clickedOnCard && !clickedButton)
                {
                    _isMarketOpen = false;
                }
            }
        }

        private bool HandleLeftClick()
        {
            if (_uiManager != null && _uiManager.IsMarketButtonHovered(_inputManager))
            {
                _isMarketOpen = !_isMarketOpen;
                return true;
            }
            if (!_isMarketOpen && !_actionSystem.IsTargeting())
            {
                if (CheckActionButtons()) return true;
            }
            return false;
        }

        private bool CheckActionButtons()
        {
            if (_uiManager == null) return false;
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
            if (_game == null) return;

            // 1. Draw Map using new Renderer
            // We need to calculate what is hovered to highlight it
            MapNode hoveredNode = _mapManager.GetNodeAt(_inputManager.MousePosition);
            Site hoveredSite = _mapManager.GetSiteAt(_inputManager.MousePosition);

            _mapRenderer.Draw(spriteBatch, _mapManager, hoveredNode, hoveredSite);

            // 2. Draw Cards (Legacy Draw)
            DrawCards(spriteBatch);

            if (_isMarketOpen)
            {
                _uiManager.DrawMarketOverlay(spriteBatch);
                _marketManager.Draw(spriteBatch, _defaultFont);
            }

            _uiManager.DrawMarketButton(spriteBatch, _isMarketOpen);
            _uiManager.DrawActionButtons(spriteBatch, _activePlayer);
            _uiManager.DrawTopBar(spriteBatch, _activePlayer);
            DrawTargetingHint(spriteBatch);
            GameLogger.Draw(spriteBatch, _smallFont);
        }

        private void DrawCards(SpriteBatch spriteBatch)
        {
            foreach (var card in _activePlayer.Hand) card.Draw(spriteBatch, _defaultFont);
            foreach (var card in _activePlayer.PlayedCards) card.Draw(spriteBatch, _defaultFont);
        }

        private void DrawTargetingHint(SpriteBatch spriteBatch)
        {
            if (!_actionSystem.IsTargeting() || _defaultFont == null) return;
            string targetText = GetTargetingText(_actionSystem.CurrentState);
            Vector2 mousePos = _inputManager.MousePosition;
            spriteBatch.DrawString(_defaultFont, targetText, mousePos + new Vector2(20, 20), Color.Red);
        }

        internal string GetTargetingText(ActionState state)
        {
            return state switch
            {
                ActionState.TargetingAssassinate => "CLICK TROOP TO KILL",
                ActionState.TargetingPlaceSpy => "CLICK SITE TO PLACE SPY",
                ActionState.TargetingReturnSpy => "CLICK SITE TO HUNT SPY",
                ActionState.TargetingReturn => "CLICK TROOP TO RETURN",
                ActionState.TargetingSupplant => "CLICK TROOP TO SUPPLANT",
                _ => "TARGETING..."
            };
        }

        private void ArrangeHandVisuals()
        {
            if (_game == null) return;
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

        internal void PlayCard(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.Assassinate) { _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card); return; }
                else if (effect.Type == EffectType.ReturnUnit) { _actionSystem.StartTargeting(ActionState.TargetingReturn, card); return; }
                else if (effect.Type == EffectType.Supplant) { _actionSystem.StartTargeting(ActionState.TargetingSupplant, card); return; }
                else if (effect.Type == EffectType.PlaceSpy) { _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy, card); return; }
            }
            ResolveCardEffects(card);
            MoveCardToPlayed(card);
        }

        internal void ResolveCardEffects(Card card)
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

        internal void MoveCardToPlayed(Card card)
        {
            _activePlayer.Hand.Remove(card);
            _activePlayer.PlayedCards.Add(card);
            ArrangeHandVisuals();
        }

        internal void EndTurn()
        {
            if (_actionSystem.IsTargeting()) _actionSystem.CancelTargeting();
            GameLogger.Log("--- TURN ENDED ---", LogChannel.General);
            _activePlayer.CleanUpTurn();
            _mapManager.DistributeControlRewards(_activePlayer);
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();
        }
    }
}