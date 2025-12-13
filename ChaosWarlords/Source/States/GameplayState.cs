using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
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
        private UIRenderer _uiRenderer;
        internal MapManager _mapManager;
        internal MarketManager _marketManager;
        internal ActionSystem _actionSystem;
        internal Player _activePlayer;
        internal bool _isMarketOpen = false;

        internal int _handY;
        internal int _playedY;

        // Views
        private MapRenderer _mapRenderer;
        private CardRenderer _cardRenderer;

        public GameplayState(Game game)
        {
            _game = game;
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
            _uiManager = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            // Create Renderers
            _mapRenderer = new MapRenderer(_pixelTexture, _pixelTexture, _defaultFont);
            _cardRenderer = new CardRenderer(_pixelTexture, _defaultFont);
            _uiRenderer = new UIRenderer(graphicsDevice, _defaultFont, _smallFont);

            // Paths
            string cardPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "cards.json");
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "data", "map.json");

            // Build World (No textures needed for logic anymore!)
            var builder = new WorldBuilder(cardPath, mapPath);
            var worldData = builder.Build();

            _activePlayer = worldData.Player;
            _marketManager = worldData.MarketManager;
            _mapManager = worldData.MapManager;
            _actionSystem = worldData.ActionSystem;

            int screenH = graphicsDevice.Viewport.Height;
            _handY = screenH - Card.Height - 20; // Bottom margin
            _playedY = _handY - (Card.Height / 2);

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
            Point mousePos = _inputManager.MousePosition.ToPoint();

            // 1. Update Card Hover State & Interactions
            // Iterate backwards to handle overlapping cards correctly (topmost first)
            for (int i = _activePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = _activePlayer.Hand[i];
                card.IsHovered = card.Bounds.Contains(mousePos);

                if (!clickHandled && _inputManager.IsLeftMouseJustClicked() && card.IsHovered)
                {
                    PlayCard(card);
                    clickHandled = true;
                    // Keep 'break' to only click one card at a time
                    break;
                }
            }

            // 2. Handle Map Interaction if no card was clicked
            if (!clickHandled && _inputManager.IsLeftMouseJustClicked())
            {
                // Check UI Buttons first
                if (CheckActionButtons()) return;
                if (CheckMarketButton()) return;

                // Check Map Nodes
                var clickedNode = _mapManager.GetNodeAt(_inputManager.MousePosition);
                if (clickedNode != null)
                {
                    _mapManager.TryDeploy(_activePlayer, clickedNode);
                }
            }
        }

        internal void UpdateTargetingLogic()
        {
            // FIX START: Allow Right-Click to Cancel Targeting
            if (_inputManager.IsRightMouseJustClicked())
            {
                _actionSystem.CancelTargeting();
                return;
            }
            // FIX END

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
            // 1. Update Hover States
            _marketManager.Update(_inputManager.GetMouseState(), _activePlayer);

            // 2. Handle Clicks
            if (_inputManager.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                Card cardToBuy = null;

                // Identify if a card was clicked
                foreach (var card in _marketManager.MarketRow)
                {
                    if (card.IsHovered)
                    {
                        clickedOnCard = true;
                        cardToBuy = card;
                        break; // Only buy one card at a time
                    }
                }

                // Attempt to Buy
                if (cardToBuy != null)
                {
                    bool success = _marketManager.TryBuyCard(_activePlayer, cardToBuy);

                    if (success)
                    {
                        GameLogger.Log($"Bought {cardToBuy.Name} for {cardToBuy.Cost} Influence.", LogChannel.Economy);
                    }
                    else
                    {
                        // Log failure reason (usually funds)
                        if (_activePlayer.Influence < cardToBuy.Cost)
                        {
                            GameLogger.Log($"Cannot afford {cardToBuy.Name}. Need {cardToBuy.Cost}, Have {_activePlayer.Influence}.", LogChannel.Economy);
                        }
                        else
                        {
                            GameLogger.Log($"Could not buy {cardToBuy.Name} (Unknown Reason).", LogChannel.Error);
                        }
                    }
                }

                bool clickedButton = _uiManager != null && _uiManager.IsMarketButtonHovered(_inputManager);

                // Close market if clicking outside cards and button
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

            // We do NOT call ArrangeHandVisuals() here.
            // This ensures the remaining cards in your hand stay exactly where they are.

            // Use the cached variable (Test friendly!)
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

            // Use the cached variable
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

            // 3. Draw Market
            if (_isMarketOpen)
            {
                // Handled inside UIRenderer for overlay, or loop here for cards
                // Ideally, UIRenderer draws the background/overlay, then you draw cards on top
                foreach (var card in _marketManager.MarketRow)
                {
                    _cardRenderer.Draw(spriteBatch, card);
                }
            }

            // 4. Draw UI
            _uiRenderer.Draw(spriteBatch, _uiManager, _activePlayer, _isMarketOpen);

            // 5. Draw Targeting Hint
            DrawTargetingHint(spriteBatch);
        }

        private void DrawCards(SpriteBatch spriteBatch)
        {
            foreach (var card in _activePlayer.Hand) _cardRenderer.Draw(spriteBatch, card);
            foreach (var card in _activePlayer.PlayedCards) _cardRenderer.Draw(spriteBatch, card);
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
                ActionState.TargetingAssassinate => "CLICK TROOP TO KILL (Right Click to Cancel)",
                ActionState.TargetingPlaceSpy => "CLICK SITE TO PLACE SPY (Right Click to Cancel)",
                ActionState.TargetingReturnSpy => "CLICK SITE TO HUNT SPY (Right Click to Cancel)",
                ActionState.TargetingReturn => "CLICK TROOP TO RETURN (Right Click to Cancel)",
                ActionState.TargetingSupplant => "CLICK TROOP TO SUPPLANT (Right Click to Cancel)",
                _ => "TARGETING..."
            };
        }

        // Helper for Unit Tests to inject mocks
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
    }
}