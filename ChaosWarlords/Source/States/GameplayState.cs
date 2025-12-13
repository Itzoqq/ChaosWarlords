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

        // Exposed for testing
        internal InputManager _inputManager;
        internal UIManager _uiManager;
        internal MapManager _mapManager;
        internal MarketManager _marketManager;
        internal ActionSystem _actionSystem;
        internal Player _activePlayer;
        internal bool _isMarketOpen = false;

        public GameplayState(Game game)
        {
            _game = game;
        }

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
            // 1. Create the Real Provider
            var inputProvider = new MonoGameInputProvider();

            // 2. Inject it into the Manager
            _inputManager = new InputManager(inputProvider);

            _uiManager = new UIManager(graphicsDevice, _defaultFont, _smallFont);

            string cardJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            CardDatabase.Load(cardJsonPath, _pixelTexture);

            _marketManager = new MarketManager();
            _marketManager.InitializeDeck(CardDatabase.GetAllMarketCards());

            _activePlayer = new Player(PlayerColor.Red);

            for (int i = 0; i < 3; i++) _activePlayer.Deck.Add(CardFactory.CreateSoldier(_pixelTexture));
            for (int i = 0; i < 7; i++) _activePlayer.Deck.Add(CardFactory.CreateNoble(_pixelTexture));
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals();

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

            if (_mapManager.Sites != null)
            {
                foreach (var site in _mapManager.Sites)
                {
                    if (site.Name.ToLower().Contains("city of gold"))
                        site.Spies.Add(PlayerColor.Blue);
                }
            }
        }

        public void UnloadContent() { }

        public void Update(GameTime gameTime)
        {
            if (_inputManager == null) return;

            _inputManager.Update();

            if (HandleGlobalInput()) return;

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

        internal bool HandleGlobalInput()
        {
            if (HandleKeyboardInput()) return true;
            if (HandleMouseInput()) return true;
            return false;
        }

        private bool HandleKeyboardInput()
        {
            if (_inputManager.IsKeyJustPressed(Keys.Escape))
            {
                if (_game != null) _game.Exit();
                return true;
            }

            if (_inputManager.IsKeyJustPressed(Keys.Enter))
            {
                EndTurn();
                return true;
            }
            return false;
        }

        private bool HandleMouseInput()
        {
            if (_inputManager.IsRightMouseJustClicked() && _actionSystem.IsTargeting())
            {
                _actionSystem.CancelTargeting();
                return true;
            }

            if (_inputManager.IsLeftMouseJustClicked())
            {
                return HandleLeftClick();
            }

            return false;
        }

        internal bool HandleLeftClick()
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

            _mapManager.Draw(spriteBatch, _defaultFont);
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

        internal void UpdateMarketLogic()
        {
            _marketManager.Update(_inputManager.GetMouseState(), _activePlayer);
            if (_inputManager.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                foreach (var card in _marketManager.MarketRow) { if (card.IsHovered) clickedOnCard = true; }

                // --- FIX: Check if _uiManager is null before accessing ---
                bool clickedButton = _uiManager != null && _uiManager.IsMarketButtonHovered(_inputManager);

                if (!clickedOnCard && !clickedButton)
                {
                    _isMarketOpen = false;
                }
            }
        }

        internal void UpdateNormalGameplay(GameTime gameTime)
        {
            bool clickHandled = false;

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
                _mapManager.Update(_inputManager.GetMouseState());
                if (_inputManager.IsLeftMouseJustClicked())
                {
                    _mapManager.TryDeploy(_activePlayer);
                }
            }

            foreach (var card in _activePlayer.PlayedCards) card.Update(gameTime, _inputManager.GetMouseState());
        }

        internal void UpdateTargetingLogic()
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

        internal void PlayCard(Card card)
        {
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