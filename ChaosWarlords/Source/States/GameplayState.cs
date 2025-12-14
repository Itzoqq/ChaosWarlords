using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
using System.IO;
using System;
using System.Linq;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IState
    {
        private readonly Game _game;
        private readonly IInputProvider _inputProvider; // Store the dependency

        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;

        internal InputManager _inputManager;
        internal UIManager _uiManager;
        internal UIRenderer _uiRenderer;
        internal MapManager _mapManager;
        internal MarketManager _marketManager;
        internal ActionSystem _actionSystem;
        internal Player _activePlayer;
        internal bool _isMarketOpen = false;

        internal int _handY;
        internal int _playedY;

        private MapRenderer _mapRenderer;
        private CardRenderer _cardRenderer;

        // Constructor Injection: We require an InputProvider to exist
        public GameplayState(Game game, IInputProvider inputProvider)
        {
            _game = game;
            _inputProvider = inputProvider;
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

            // Use the injected provider instead of creating a new hardware one
            _inputManager = new InputManager(_inputProvider);

            // --- INITIALIZATION ---
            _uiManager = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
            _uiRenderer = new UIRenderer(graphicsDevice, _defaultFont, _smallFont);

            _mapRenderer = new MapRenderer(_pixelTexture, _pixelTexture, _defaultFont);
            _cardRenderer = new CardRenderer(_pixelTexture, _defaultFont);

            var builder = new WorldBuilder("data/cards.json", "data/map.json");
            var worldData = builder.Build();

            _activePlayer = worldData.Player;
            _marketManager = worldData.MarketManager;
            _mapManager = worldData.MapManager;
            _actionSystem = worldData.ActionSystem;

            int screenH = graphicsDevice.Viewport.Height;
            _handY = screenH - Card.Height - 20;
            _playedY = _handY - (Card.Height / 2);

            ArrangeHandVisuals();
            _mapManager.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
        }

        public void UnloadContent() { }

        public void Update(GameTime gameTime)
        {
            if (_inputManager == null) return;
            _inputManager.Update();

            // Global inputs (Esc, Enter, Right-Click) take priority
            if (HandleGlobalInput()) return;

            if (_isMarketOpen) UpdateMarketLogic();
            else if (_actionSystem.CurrentState == ActionState.SelectingSpyToReturn) UpdateSpySelectionLogic();
            else if (_actionSystem.IsTargeting()) UpdateTargetingLogic();
            else UpdateNormalGameplay(gameTime);
        }

        internal bool HandleGlobalInput()
        {
            // Exit Game
            if (_inputManager.IsKeyJustPressed(Keys.Escape)) { if (_game != null) _game.Exit(); return true; }

            // End Turn
            if (_inputManager.IsKeyJustPressed(Keys.Enter)) { EndTurn(); return true; }

            // --- RESTORED: RIGHT-CLICK TO CANCEL ---
            if (_inputManager.IsRightMouseJustClicked())
            {
                // Priority 1: Close Market if open
                if (_isMarketOpen)
                {
                    _isMarketOpen = false;
                    return true;
                }

                // Priority 2: Cancel Targeting/Action if active
                if (_actionSystem.IsTargeting())
                {
                    _actionSystem.CancelTargeting();
                    return true;
                }
            }

            return false;
        }

        internal void UpdateNormalGameplay(GameTime gameTime)
        {
            Point mousePos = _inputManager.MousePosition.ToPoint();

            if (HandleCardInput(mousePos)) return;

            if (_inputManager.IsLeftMouseJustClicked())
            {
                HandleWorldInput();
            }
        }

        private bool HandleCardInput(Point mousePos)
        {
            bool cardClicked = false;
            for (int i = _activePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = _activePlayer.Hand[i];
                card.IsHovered = card.Bounds.Contains(mousePos);

                if (!cardClicked && _inputManager.IsLeftMouseJustClicked() && card.IsHovered)
                {
                    PlayCard(card);
                    cardClicked = true;
                }
            }
            return cardClicked;
        }

        private void HandleWorldInput()
        {
            if (CheckActionButtons()) return;
            if (CheckMarketButton()) return;
            HandleMapInteraction();
        }

        private void HandleMapInteraction()
        {
            var clickedNode = _mapManager.GetNodeAt(_inputManager.MousePosition);
            if (clickedNode != null)
            {
                _mapManager.TryDeploy(_activePlayer, clickedNode);
            }
        }

        // --- SPY SELECTION LOGIC ---
        internal void UpdateSpySelectionLogic()
        {
            // Allow Right-Click to cancel selection (Handled in HandleGlobalInput), 
            // but we also check for left clicks here.
            if (!_inputManager.IsLeftMouseJustClicked()) return;

            Site site = _actionSystem.PendingSite;
            if (site == null)
            {
                _actionSystem.CancelTargeting();
                return;
            }

            var enemies = _mapManager.GetEnemySpiesAtSite(site, _activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (_inputManager.IsMouseOver(btnRect))
                {
                    bool success = _actionSystem.FinalizeSpyReturn(enemies[i]);
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
                    return;
                }
            }

            // Clicking empty space cancels the specific selection but keeps targeting mode? 
            // Usually easier to just cancel everything or do nothing. 
            // Let's cancel targeting to be safe.
            GameLogger.Log("Cancelled selection.", LogChannel.General);
            _actionSystem.CancelTargeting();
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

        internal void UpdateMarketLogic()
        {
            _marketManager.Update(_inputManager.GetMouseState(), _activePlayer);

            if (_inputManager.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                Card cardToBuy = null;

                foreach (var card in _marketManager.MarketRow)
                {
                    if (card.IsHovered)
                    {
                        clickedOnCard = true;
                        cardToBuy = card;
                        break;
                    }
                }

                if (cardToBuy != null)
                {
                    bool success = _marketManager.TryBuyCard(_activePlayer, cardToBuy);
                    if (success) GameLogger.Log($"Bought {cardToBuy.Name}.", LogChannel.Economy);
                }

                bool clickedButton = _uiManager != null && _uiManager.IsMarketButtonHovered(_inputManager);

                if (!clickedOnCard && !clickedButton)
                {
                    _isMarketOpen = false;
                }
            }
        }

        private bool CheckMarketButton()
        {
            if (_uiManager != null && _uiManager.IsMarketButtonHovered(_inputManager))
            {
                _isMarketOpen = !_isMarketOpen;
                return true;
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
            card.Position = new Vector2(card.Position.X, _playedY);
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

        private void ArrangeHandVisuals()
        {
            if (_game == null) return;
            int cardWidth = Card.Width;
            int gap = 10;
            int totalHandWidth = (_activePlayer.Hand.Count * cardWidth) + ((_activePlayer.Hand.Count - 1) * gap);
            int startX = (_game.GraphicsDevice.Viewport.Width - totalHandWidth) / 2;
            for (int i = 0; i < _activePlayer.Hand.Count; i++)
            {
                _activePlayer.Hand[i].Position = new Vector2(startX + (i * (cardWidth + gap)), _handY);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_game == null) return;

            // 1. Draw Map
            MapNode hoveredNode = _mapManager.GetNodeAt(_inputManager.MousePosition);
            Site hoveredSite = _mapManager.GetSiteAt(_inputManager.MousePosition);
            _mapRenderer.Draw(spriteBatch, _mapManager, hoveredNode, hoveredSite);

            // 2. Draw Cards
            DrawCards(spriteBatch);

            // 3. Draw Market (USING UI RENDERER)
            if (_isMarketOpen)
            {
                _uiRenderer.DrawMarketOverlay(spriteBatch, _uiManager.ScreenWidth, _uiManager.ScreenHeight);
                foreach (var card in _marketManager.MarketRow)
                {
                    _cardRenderer.Draw(spriteBatch, card);
                }
            }

            // 4. Draw UI (USING UI RENDERER)
            _uiRenderer.DrawMarketButton(spriteBatch, _uiManager, _isMarketOpen);
            _uiRenderer.DrawActionButtons(spriteBatch, _uiManager, _activePlayer);
            _uiRenderer.DrawTopBar(spriteBatch, _activePlayer, _uiManager.ScreenWidth);
            DrawTargetingHint(spriteBatch);

            // 5. Draw Selection Overlay (NEW)
            if (_actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                DrawSpySelectionUI(spriteBatch);
            }
        }

        private void DrawCards(SpriteBatch spriteBatch)
        {
            foreach (var card in _activePlayer.Hand) _cardRenderer.Draw(spriteBatch, card);
            foreach (var card in _activePlayer.PlayedCards) _cardRenderer.Draw(spriteBatch, card);
        }

        private void DrawTargetingHint(SpriteBatch spriteBatch)
        {
            if (!_actionSystem.IsTargeting() || _defaultFont == null) return;
            if (_actionSystem.CurrentState == ActionState.SelectingSpyToReturn) return;

            string targetText = GetTargetingText(_actionSystem.CurrentState);
            Vector2 mousePos = _inputManager.MousePosition;
            spriteBatch.DrawString(_defaultFont, targetText, mousePos + new Vector2(20, 20), Color.Red);
        }

        private void DrawSpySelectionUI(SpriteBatch spriteBatch)
        {
            Site site = _actionSystem.PendingSite;
            if (site == null) return;

            var enemies = _mapManager.GetEnemySpiesAtSite(site, _activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            spriteBatch.DrawString(_smallFont, "CHOOSE SPY TO REMOVE:", startPos + new Vector2(0, -20), Color.White);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                Color btnColor = Color.Gray;
                if (enemies[i] == PlayerColor.Red) btnColor = Color.Red;
                if (enemies[i] == PlayerColor.Blue) btnColor = Color.Blue;
                if (enemies[i] == PlayerColor.Neutral) btnColor = Color.White;

                if (_inputManager.IsMouseOver(btnRect)) btnColor = Color.Lerp(btnColor, Color.White, 0.5f);

                spriteBatch.Draw(_pixelTexture, btnRect, btnColor);
                UIRenderer.DrawBorder(spriteBatch, _pixelTexture, btnRect, 2, Color.Black);
            }
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

        internal void InjectDependencies(InputManager input, UIManager ui, MapManager map, MarketManager market, ActionSystem action, Player player)
        {
            _inputManager = input;
            _uiManager = ui;
            _mapManager = map;
            _marketManager = market;
            _actionSystem = action;
            _activePlayer = player;
        }
    }
}