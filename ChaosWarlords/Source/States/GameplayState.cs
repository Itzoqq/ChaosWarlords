using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.States.Input;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IGameplayState
    {
        private readonly Game _game;
        private readonly IInputProvider _inputProvider;
        private readonly ICardDatabase _cardDatabase;

        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;

        internal InputManager _inputManagerBacking;
        internal IUISystem _uiManagerBacking;
        internal IMapManager _mapManagerBacking;
        internal IMarketManager _marketManagerBacking;
        internal IActionSystem _actionSystemBacking;
        internal TurnManager _turnManagerBacking;
        internal bool _isMarketOpenBacking = false;

        internal int _handYBacking;
        internal int _playedYBacking;

        private MapRenderer _mapRenderer;
        private CardRenderer _cardRenderer;
        internal UIRenderer _uiRenderer;

        private List<CardViewModel> _handViewModels = new List<CardViewModel>();
        private List<CardViewModel> _marketViewModels = new List<CardViewModel>();
        private List<CardViewModel> _playedViewModels = new List<CardViewModel>();

        // --- Properties ---
        public InputManager InputManager => _inputManagerBacking;
        public IUISystem UIManager => _uiManagerBacking;
        public IMapManager MapManager => _mapManagerBacking;
        public IMarketManager MarketManager => _marketManagerBacking;
        public IActionSystem ActionSystem => _actionSystemBacking;
        public TurnManager TurnManager => _turnManagerBacking;
        public IInputMode InputMode { get; set; }

        internal List<CardViewModel> HandViewModels => _handViewModels;
        internal List<CardViewModel> MarketViewModels => _marketViewModels;
        internal List<CardViewModel> PlayedViewModels => _playedViewModels;

        public int HandY => _handYBacking;
        public int PlayedY => _playedYBacking;

        public bool IsMarketOpen
        {
            get => _isMarketOpenBacking;
            set
            {
                _isMarketOpenBacking = value;
                if (_isMarketOpenBacking)
                {
                    InputMode = new MarketInputMode(this, _inputManagerBacking, _uiManagerBacking, _marketManagerBacking, _turnManagerBacking);
                }
                else
                {
                    SwitchToNormalMode();
                }
            }
        }

        public GameplayState(Game game, IInputProvider inputProvider, ICardDatabase cardDatabase)
        {
            _game = game;
            _inputProvider = inputProvider;
            _cardDatabase = cardDatabase;
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

            _inputManagerBacking = new InputManager(_inputProvider);
            _uiManagerBacking = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            _uiRenderer = new UIRenderer(graphicsDevice, _defaultFont, _smallFont);
            _mapRenderer = new MapRenderer(_pixelTexture, _pixelTexture, _defaultFont);
            _cardRenderer = new CardRenderer(_pixelTexture, _defaultFont);

            var builder = new WorldBuilder(_cardDatabase, "data/map.json");
            var worldData = builder.Build();

            _turnManagerBacking = worldData.TurnManager;
            _actionSystemBacking = worldData.ActionSystem;
            _marketManagerBacking = worldData.MarketManager;
            _mapManagerBacking = worldData.MapManager;

            _actionSystemBacking.SetCurrentPlayer(_turnManagerBacking.ActivePlayer);
            _turnManagerBacking.ActivePlayer.DrawCards(5);

            // FIX: Increase gap between Hand and PlayedY to prevent overlap
            int screenH = graphicsDevice.Viewport.Height;
            _handYBacking = screenH - Card.Height - 20;
            _playedYBacking = _handYBacking - Card.Height - 10; // Full card height gap

            InitializeEventSubscriptions();

            _mapManagerBacking.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
            SyncHandVisuals();
            SwitchToNormalMode();
        }

        public void UnloadContent() { }

        public void Update(GameTime gameTime)
        {
            _inputManagerBacking.Update();
            _uiManagerBacking.Update(_inputManagerBacking);

            if (HandleGlobalInput()) return;

            // FIX: Handle Spy Selection Input specifically (it's a modal UI state)
            if (_actionSystemBacking.CurrentState == ActionState.SelectingSpyToReturn)
            {
                HandleSpySelectionInput();
                // Return early to block other inputs (like playing cards) while selecting spy
                return;
            }

            SyncHandVisuals();
            SyncMarketVisuals();
            SyncPlayedVisuals();

            UpdateVisualsHover(_handViewModels);
            if (IsMarketOpen) UpdateVisualsHover(_marketViewModels);

            IGameCommand command = InputMode.HandleInput(
                _inputManagerBacking,
                _marketManagerBacking,
                _mapManagerBacking,
                _turnManagerBacking.ActivePlayer,
                _actionSystemBacking);

            command?.Execute(this);
        }

        private void SyncHandVisuals()
        {
            if (_turnManagerBacking == null) return;
            var hand = _turnManagerBacking.ActivePlayer.Hand;

            _handViewModels.RemoveAll(vm => !hand.Contains(vm.Model));
            foreach (var card in hand)
            {
                if (!_handViewModels.Any(vm => vm.Model == card))
                    _handViewModels.Add(new CardViewModel(card));
            }

            int cardWidth = Card.Width;
            int gap = 10;
            int totalWidth = (hand.Count * cardWidth) + ((hand.Count - 1) * gap);
            int viewportWidth = _game?.GraphicsDevice.Viewport.Width ?? 1024;
            int startX = (viewportWidth - totalWidth) / 2;

            var sortedVMs = new List<CardViewModel>();
            for (int i = 0; i < hand.Count; i++)
            {
                var vm = _handViewModels.FirstOrDefault(v => v.Model == hand[i]);
                if (vm != null)
                {
                    vm.Position = new Vector2(startX + (i * (cardWidth + gap)), _handYBacking);
                    sortedVMs.Add(vm);
                }
            }
            _handViewModels = sortedVMs;
        }

        private void SyncMarketVisuals()
        {
            if (_marketManagerBacking == null) return;
            var marketRow = _marketManagerBacking.MarketRow;

            _marketViewModels.RemoveAll(vm => !marketRow.Contains(vm.Model));
            foreach (var card in marketRow)
            {
                if (!_marketViewModels.Any(vm => vm.Model == card))
                    _marketViewModels.Add(new CardViewModel(card));
            }

            int startX = 100;
            int startY = 100;
            int gap = 10;
            for (int i = 0; i < marketRow.Count; i++)
            {
                var vm = _marketViewModels.FirstOrDefault(v => v.Model == marketRow[i]);
                if (vm != null)
                {
                    vm.Position = new Vector2(startX + (i * (Card.Width + gap)), startY);
                }
            }
        }

        private void SyncPlayedVisuals()
        {
            if (_turnManagerBacking == null) return;
            var played = _turnManagerBacking.ActivePlayer.PlayedCards;

            _playedViewModels.RemoveAll(vm => !played.Contains(vm.Model));
            foreach (var card in played)
            {
                if (!_playedViewModels.Any(vm => vm.Model == card))
                    _playedViewModels.Add(new CardViewModel(card));
            }

            // Position played cards. 
            // To prevent them from "jumping" too much, we center them similarly to the hand.
            int cardWidth = Card.Width;
            int gap = 10;
            int totalWidth = (played.Count * cardWidth) + ((played.Count - 1) * gap);
            int viewportWidth = _game?.GraphicsDevice.Viewport.Width ?? 1024;
            int startX = (viewportWidth - totalWidth) / 2;

            for (int i = 0; i < played.Count; i++)
            {
                var vm = _playedViewModels.FirstOrDefault(v => v.Model == played[i]);
                if (vm != null)
                {
                    vm.Position = new Vector2(startX + (i * (cardWidth + gap)), _playedYBacking);
                }
            }
        }

        private void UpdateVisualsHover(List<CardViewModel> vms)
        {
            Point mousePos = _inputManagerBacking.MousePosition.ToPoint();
            bool foundHovered = false;
            for (int i = vms.Count - 1; i >= 0; i--)
            {
                var vm = vms[i];
                if (!foundHovered && vm.Bounds.Contains(mousePos))
                {
                    vm.IsHovered = true;
                    foundHovered = true;
                }
                else
                {
                    vm.IsHovered = false;
                }
            }
        }

        public void PlayCard(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    _actionSystemBacking.StartTargeting(GetTargetingState(effect.Type), card);
                    SwitchToTargetingMode();
                    return;
                }
            }
            _turnManagerBacking.PlayCard(card);
            ResolveCardEffects(card);
            MoveCardToPlayed(card);
        }

        public void ResolveCardEffects(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.GainResource)
                {
                    if (effect.TargetResource == ResourceType.Power) _turnManagerBacking.ActivePlayer.Power += effect.Amount;
                    if (effect.TargetResource == ResourceType.Influence) _turnManagerBacking.ActivePlayer.Influence += effect.Amount;
                }
            }
        }

        public void MoveCardToPlayed(Card card)
        {
            _turnManagerBacking.ActivePlayer.Hand.Remove(card);
            _turnManagerBacking.ActivePlayer.PlayedCards.Add(card);
        }

        public void EndTurn()
        {
            if (_actionSystemBacking.IsTargeting()) _actionSystemBacking.CancelTargeting();

            _mapManagerBacking.DistributeControlRewards(_turnManagerBacking.ActivePlayer);
            _turnManagerBacking.ActivePlayer.CleanUpTurn();
            _turnManagerBacking.EndTurn();
            _turnManagerBacking.ActivePlayer.DrawCards(5);
            _actionSystemBacking.SetCurrentPlayer(_turnManagerBacking.ActivePlayer);
            SyncHandVisuals();
        }

        public void ToggleMarket() { IsMarketOpen = !IsMarketOpen; }
        public void CloseMarket() { IsMarketOpen = false; }
        public void SwitchToTargetingMode() { InputMode = new TargetingInputMode(this, _inputManagerBacking, _uiManagerBacking, _mapManagerBacking, _turnManagerBacking, _actionSystemBacking); }
        public void SwitchToNormalMode() { InputMode = new NormalPlayInputMode(this, _inputManagerBacking, _uiManagerBacking, _mapManagerBacking, _turnManagerBacking, _actionSystemBacking); }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_game == null) return;

            MapNode hoveredNode = _mapManagerBacking.GetNodeAt(_inputManagerBacking.MousePosition);
            Site hoveredSite = _mapManagerBacking.GetSiteAt(_inputManagerBacking.MousePosition);
            _mapRenderer.Draw(spriteBatch, _mapManagerBacking, hoveredNode, hoveredSite);

            foreach (var vm in _handViewModels) _cardRenderer.Draw(spriteBatch, vm);
            foreach (var vm in _playedViewModels) _cardRenderer.Draw(spriteBatch, vm);

            if (IsMarketOpen)
            {
                _uiRenderer.DrawMarketOverlay(spriteBatch, _marketManagerBacking, UIManager.ScreenWidth, UIManager.ScreenHeight);
                foreach (var vm in _marketViewModels) _cardRenderer.Draw(spriteBatch, vm);
            }

            _uiRenderer.DrawMarketButton(spriteBatch, UIManager);
            _uiRenderer.DrawActionButtons(spriteBatch, UIManager, TurnManager.ActivePlayer);
            _uiRenderer.DrawTopBar(spriteBatch, TurnManager.ActivePlayer, UIManager.ScreenWidth);

            DrawTurnIndicator(spriteBatch);
            DrawTargetingHint(spriteBatch);

            // FIX: Restore Spy UI drawing
            if (ActionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                DrawSpySelectionUI(spriteBatch);
            }
        }

        internal bool HandleGlobalInput()
        {
            if (_inputManagerBacking.IsKeyJustPressed(Keys.Escape)) { _game.Exit(); return true; }
            if (_inputManagerBacking.IsKeyJustPressed(Keys.Enter)) { EndTurn(); return true; }
            if (_inputManagerBacking.IsRightMouseJustClicked())
            {
                if (IsMarketOpen) { IsMarketOpen = false; return true; }
                if (_actionSystemBacking.IsTargeting()) { _actionSystemBacking.CancelTargeting(); SwitchToNormalMode(); return true; }
            }
            return false;
        }

        private bool IsTargetingEffect(EffectType type)
        {
            return type == EffectType.Assassinate || type == EffectType.ReturnUnit ||
                   type == EffectType.Supplant || type == EffectType.PlaceSpy;
        }

        private ActionState GetTargetingState(EffectType type)
        {
            return type switch
            {
                EffectType.Assassinate => ActionState.TargetingAssassinate,
                EffectType.ReturnUnit => ActionState.TargetingReturn,
                EffectType.Supplant => ActionState.TargetingSupplant,
                EffectType.PlaceSpy => ActionState.TargetingPlaceSpy,
                _ => ActionState.Normal
            };
        }

        public string GetTargetingText(ActionState state) => state.ToString();

        private void InitializeEventSubscriptions()
        {
            _uiManagerBacking.OnMarketToggleRequest -= HandleMarketToggle;
            _uiManagerBacking.OnAssassinateRequest -= HandleAssassinateRequest;
            _uiManagerBacking.OnReturnSpyRequest -= HandleReturnSpyRequest;
            _actionSystemBacking.OnActionCompleted -= HandleActionCompleted;
            _actionSystemBacking.OnActionFailed -= HandleActionFailed;

            _uiManagerBacking.OnMarketToggleRequest += HandleMarketToggle;
            _uiManagerBacking.OnAssassinateRequest += HandleAssassinateRequest;
            _uiManagerBacking.OnReturnSpyRequest += HandleReturnSpyRequest;
            _actionSystemBacking.OnActionCompleted += HandleActionCompleted;
            _actionSystemBacking.OnActionFailed += HandleActionFailed;
        }

        private void HandleMarketToggle(object sender, EventArgs e) => ToggleMarket();
        private void HandleAssassinateRequest(object sender, EventArgs e) { _actionSystemBacking.TryStartAssassinate(); if (_actionSystemBacking.IsTargeting()) SwitchToTargetingMode(); }
        private void HandleReturnSpyRequest(object sender, EventArgs e) { _actionSystemBacking.TryStartReturnSpy(); if (_actionSystemBacking.IsTargeting()) SwitchToTargetingMode(); }
        private void HandleActionFailed(object sender, string msg) => GameLogger.Log(msg, LogChannel.Error);

        private void HandleActionCompleted(object sender, EventArgs e)
        {
            if (_actionSystemBacking.PendingCard != null)
            {
                ResolveCardEffects(_actionSystemBacking.PendingCard);
                MoveCardToPlayed(_actionSystemBacking.PendingCard);
            }
            _actionSystemBacking.CancelTargeting();
            SwitchToNormalMode();
        }

        private void DrawTurnIndicator(SpriteBatch sb)
        {
            // FIX: Use Active Player Color
            Color c = TurnManager.ActivePlayer.Color == PlayerColor.Red ? Color.Red : Color.Blue;
            string text = $"-- {TurnManager.ActivePlayer.Color}'s Turn --";
            sb.DrawString(_defaultFont, text, new Vector2(20, 50), c);
        }

        private void DrawTargetingHint(SpriteBatch sb)
        {
            if (!ActionSystem.IsTargeting()) return;
            string text = GetTargetingText(ActionSystem.CurrentState);
            sb.DrawString(_defaultFont, text, _inputManagerBacking.MousePosition + new Vector2(20, 20), Color.Red);
        }

        // --- NEW: DrawSpySelectionUI Logic Restored ---
        private void DrawSpySelectionUI(SpriteBatch sb)
        {
            var site = _actionSystemBacking.PendingSite;
            if (site == null) return;

            string header = "Select Spy to Return:";
            Vector2 size = _defaultFont.MeasureString(header);
            Vector2 startPos = new Vector2((UIManager.ScreenWidth - size.X) / 2, 200);

            sb.DrawString(_defaultFont, header, startPos, Color.White);

            int yOffset = 40;
            foreach (var spy in site.Spies)
            {
                string btnText = spy.ToString();
                Rectangle rect = new Rectangle((int)startPos.X, (int)startPos.Y + yOffset, 200, 30);

                sb.Draw(_pixelTexture, rect, Color.Gray);
                sb.DrawString(_defaultFont, btnText, new Vector2(rect.X + 10, rect.Y + 5), Color.Black);

                yOffset += 40;
            }
        }

        // --- NEW: HandleSpySelectionInput Logic ---
        private void HandleSpySelectionInput()
        {
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;

            var site = _actionSystemBacking.PendingSite;
            if (site == null) return;

            Vector2 headerSize = _defaultFont.MeasureString("Select Spy to Return:");
            float drawX = (UIManager.ScreenWidth - headerSize.X) / 2;
            Vector2 startPos = new Vector2(drawX, 200);

            int yOffset = 40;
            Point mousePos = _inputManagerBacking.MousePosition.ToPoint();

            foreach (var spy in site.Spies.ToList()) // ToList to avoid modification errors if list changes
            {
                Rectangle rect = new Rectangle((int)drawX, (int)startPos.Y + yOffset, 200, 30);
                if (rect.Contains(mousePos))
                {
                    _actionSystemBacking.FinalizeSpyReturn(spy); // FIX: Correct method name
                    return;
                }
                yOffset += 40;
            }
        }

        public void InjectDependencies(
            InputManager input,
            IUISystem ui,
            IMapManager map,
            IMarketManager market,
            IActionSystem action,
            TurnManager turnManager)
        {
            _inputManagerBacking = input;
            _uiManagerBacking = ui;
            _mapManagerBacking = map;
            _marketManagerBacking = market;
            _actionSystemBacking = action;
            _turnManagerBacking = turnManager;
            _actionSystemBacking.SetCurrentPlayer(_turnManagerBacking.ActivePlayer);
            InitializeEventSubscriptions();
        }

        public void ArrangeHandVisuals() { SyncHandVisuals(); } // Compat
        public Card GetHoveredHandCard() => _handViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
        public Card GetHoveredMarketCard() => _marketViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
    }
}