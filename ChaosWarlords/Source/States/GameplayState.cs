using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
using ChaosWarlords.Source.Contexts;
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

        // --- Visual Assets ---
        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;
        private MapRenderer _mapRenderer;
        private CardRenderer _cardRenderer;
        internal UIRenderer _uiRenderer;

        // --- Context & Systems ---
        internal MatchContext _matchContext;

        internal InputManager _inputManagerBacking;
        internal IUISystem _uiManagerBacking;

        // --- State Variables ---
        internal bool _isMarketOpenBacking = false;
        internal int _handYBacking;
        internal int _playedYBacking;

        private List<CardViewModel> _handViewModels = new List<CardViewModel>();
        private List<CardViewModel> _marketViewModels = new List<CardViewModel>();
        private List<CardViewModel> _playedViewModels = new List<CardViewModel>();

        // --- Properties ---
        public InputManager InputManager => _inputManagerBacking;
        public IUISystem UIManager => _uiManagerBacking;

        public IMapManager MapManager => _matchContext?.MapManager;
        public IMarketManager MarketManager => _matchContext?.MarketManager;
        public IActionSystem ActionSystem => _matchContext?.ActionSystem;
        public TurnManager TurnManager => _matchContext?.TurnManager as TurnManager;
        public MatchContext MatchContext => _matchContext;

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
                    InputMode = new MarketInputMode(
                        this,
                        _inputManagerBacking,
                        _uiManagerBacking,
                        _matchContext.MarketManager,
                        _matchContext.TurnManager as TurnManager
                    );
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

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase
            );

            _matchContext.ActionSystem.SetCurrentPlayer(_matchContext.ActivePlayer);

            // FIX 1: Draw cards for ALL players at the start, not just the active one.
            // This ensures Player 2 has a hand ready when the turn passes to them.
            if (_matchContext.TurnManager.Players != null)
            {
                foreach (var player in _matchContext.TurnManager.Players)
                {
                    player.DrawCards(5);
                }
            }

            int screenH = graphicsDevice.Viewport.Height;
            _handYBacking = screenH - Card.Height - 20;
            _playedYBacking = _handYBacking - Card.Height - 10;

            InitializeEventSubscriptions();

            _matchContext.MapManager.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
            SyncHandVisuals();
            SwitchToNormalMode();
        }

        public void UnloadContent() { }

        public void Update(GameTime gameTime)
        {
            _inputManagerBacking.Update();
            _uiManagerBacking.Update(_inputManagerBacking);

            if (HandleGlobalInput()) return;

            if (_matchContext.ActionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                HandleSpySelectionInput();
                return;
            }

            SyncHandVisuals();
            SyncMarketVisuals();
            SyncPlayedVisuals();

            UpdateVisualsHover(_handViewModels);
            if (IsMarketOpen) UpdateVisualsHover(_marketViewModels);

            IGameCommand command = InputMode.HandleInput(
                _inputManagerBacking,
                _matchContext.MarketManager,
                _matchContext.MapManager,
                _matchContext.ActivePlayer,
                _matchContext.ActionSystem);

            command?.Execute(this);
        }

        private void SyncHandVisuals()
        {
            if (_matchContext == null) return;
            var hand = _matchContext.ActivePlayer.Hand;

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
            if (_matchContext == null) return;
            var marketRow = _matchContext.MarketManager.MarketRow;

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
            if (_matchContext == null) return;
            var played = _matchContext.ActivePlayer.PlayedCards;

            _playedViewModels.RemoveAll(vm => !played.Contains(vm.Model));
            foreach (var card in played)
            {
                if (!_playedViewModels.Any(vm => vm.Model == card))
                    _playedViewModels.Add(new CardViewModel(card));
            }

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
                    _matchContext.ActionSystem.StartTargeting(GetTargetingState(effect.Type), card);
                    SwitchToTargetingMode();
                    return;
                }
            }
            _matchContext.TurnManager.PlayCard(card);
            ResolveCardEffects(card);
            MoveCardToPlayed(card);
        }

        public void ResolveCardEffects(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.GainResource)
                {
                    if (effect.TargetResource == ResourceType.Power) _matchContext.ActivePlayer.Power += effect.Amount;
                    if (effect.TargetResource == ResourceType.Influence) _matchContext.ActivePlayer.Influence += effect.Amount;
                }
            }
        }

        public void MoveCardToPlayed(Card card)
        {
            _matchContext.ActivePlayer.Hand.Remove(card);
            _matchContext.ActivePlayer.PlayedCards.Add(card);
        }

        public void EndTurn()
        {
            if (_matchContext.ActionSystem.IsTargeting()) _matchContext.ActionSystem.CancelTargeting();

            _matchContext.MapManager.DistributeControlRewards(_matchContext.ActivePlayer);
            _matchContext.ActivePlayer.CleanUpTurn();

            // FIX 2: Draw Cards BEFORE switching the turn.
            // The active player refills THEIR hand, then passes the turn.
            _matchContext.ActivePlayer.DrawCards(5);

            _matchContext.TurnManager.EndTurn();

            _matchContext.ActionSystem.SetCurrentPlayer(_matchContext.ActivePlayer);
            SyncHandVisuals();
        }

        public void ToggleMarket() { IsMarketOpen = !IsMarketOpen; }
        public void CloseMarket() { IsMarketOpen = false; }

        public void SwitchToTargetingMode()
        {
            InputMode = new TargetingInputMode(
                this,
                _inputManagerBacking,
                _uiManagerBacking,
                _matchContext.MapManager,
                _matchContext.TurnManager as TurnManager,
                _matchContext.ActionSystem
            );
        }

        public void SwitchToNormalMode()
        {
            InputMode = new NormalPlayInputMode(
                this,
                _inputManagerBacking,
                _uiManagerBacking,
                _matchContext.MapManager,
                _matchContext.TurnManager as TurnManager,
                _matchContext.ActionSystem
            );
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_game == null) return;

            MapNode hoveredNode = _matchContext.MapManager.GetNodeAt(_inputManagerBacking.MousePosition);
            Site hoveredSite = _matchContext.MapManager.GetSiteAt(_inputManagerBacking.MousePosition);
            _mapRenderer.Draw(spriteBatch, _matchContext.MapManager, hoveredNode, hoveredSite);

            foreach (var vm in _handViewModels) _cardRenderer.Draw(spriteBatch, vm);
            foreach (var vm in _playedViewModels) _cardRenderer.Draw(spriteBatch, vm);

            if (IsMarketOpen)
            {
                _uiRenderer.DrawMarketOverlay(spriteBatch, _matchContext.MarketManager, UIManager.ScreenWidth, UIManager.ScreenHeight);
                foreach (var vm in _marketViewModels) _cardRenderer.Draw(spriteBatch, vm);
            }

            _uiRenderer.DrawMarketButton(spriteBatch, UIManager);
            _uiRenderer.DrawActionButtons(spriteBatch, UIManager, _matchContext.ActivePlayer);
            _uiRenderer.DrawTopBar(spriteBatch, _matchContext.ActivePlayer, UIManager.ScreenWidth);

            DrawTurnIndicator(spriteBatch);
            DrawTargetingHint(spriteBatch);

            if (_matchContext.ActionSystem.CurrentState == ActionState.SelectingSpyToReturn)
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
                if (_matchContext.ActionSystem.IsTargeting()) { _matchContext.ActionSystem.CancelTargeting(); SwitchToNormalMode(); return true; }
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

        internal void InitializeEventSubscriptions()
        {
            _uiManagerBacking.OnMarketToggleRequest -= HandleMarketToggle;
            _uiManagerBacking.OnAssassinateRequest -= HandleAssassinateRequest;
            _uiManagerBacking.OnReturnSpyRequest -= HandleReturnSpyRequest;
            _matchContext.ActionSystem.OnActionCompleted -= HandleActionCompleted;
            _matchContext.ActionSystem.OnActionFailed -= HandleActionFailed;

            _uiManagerBacking.OnMarketToggleRequest += HandleMarketToggle;
            _uiManagerBacking.OnAssassinateRequest += HandleAssassinateRequest;
            _uiManagerBacking.OnReturnSpyRequest += HandleReturnSpyRequest;
            _matchContext.ActionSystem.OnActionCompleted += HandleActionCompleted;
            _matchContext.ActionSystem.OnActionFailed += HandleActionFailed;
        }

        private void HandleMarketToggle(object sender, EventArgs e) => ToggleMarket();
        private void HandleAssassinateRequest(object sender, EventArgs e) { _matchContext.ActionSystem.TryStartAssassinate(); if (_matchContext.ActionSystem.IsTargeting()) SwitchToTargetingMode(); }
        private void HandleReturnSpyRequest(object sender, EventArgs e) { _matchContext.ActionSystem.TryStartReturnSpy(); if (_matchContext.ActionSystem.IsTargeting()) SwitchToTargetingMode(); }
        private void HandleActionFailed(object sender, string msg) => GameLogger.Log(msg, LogChannel.Error);

        private void HandleActionCompleted(object sender, EventArgs e)
        {
            if (_matchContext.ActionSystem.PendingCard != null)
            {
                ResolveCardEffects(_matchContext.ActionSystem.PendingCard);
                MoveCardToPlayed(_matchContext.ActionSystem.PendingCard);
            }
            _matchContext.ActionSystem.CancelTargeting();
            SwitchToNormalMode();
        }

        private void DrawTurnIndicator(SpriteBatch sb)
        {
            Color c = _matchContext.ActivePlayer.Color == PlayerColor.Red ? Color.Red : Color.Blue;
            string text = $"-- {_matchContext.ActivePlayer.Color}'s Turn --";
            sb.DrawString(_defaultFont, text, new Vector2(20, 50), c);
        }

        private void DrawTargetingHint(SpriteBatch sb)
        {
            if (!_matchContext.ActionSystem.IsTargeting()) return;
            string text = GetTargetingText(_matchContext.ActionSystem.CurrentState);
            sb.DrawString(_defaultFont, text, _inputManagerBacking.MousePosition + new Vector2(20, 20), Color.Red);
        }

        private void DrawSpySelectionUI(SpriteBatch sb)
        {
            var site = _matchContext.ActionSystem.PendingSite;
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

        private void HandleSpySelectionInput()
        {
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;

            var site = _matchContext.ActionSystem.PendingSite;
            if (site == null) return;

            Vector2 headerSize = _defaultFont.MeasureString("Select Spy to Return:");
            float drawX = (UIManager.ScreenWidth - headerSize.X) / 2;
            Vector2 startPos = new Vector2(drawX, 200);

            int yOffset = 40;
            Point mousePos = _inputManagerBacking.MousePosition.ToPoint();

            foreach (var spy in site.Spies.ToList())
            {
                Rectangle rect = new Rectangle((int)drawX, (int)startPos.Y + yOffset, 200, 30);
                if (rect.Contains(mousePos))
                {
                    _matchContext.ActionSystem.FinalizeSpyReturn(spy);
                    return;
                }
                yOffset += 40;
            }
        }

        public void ArrangeHandVisuals() { SyncHandVisuals(); }
        public Card GetHoveredHandCard() => _handViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
        public Card GetHoveredMarketCard() => _marketViewModels.FirstOrDefault(vm => vm.IsHovered)?.Model;
    }
}