using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Commands;
using System;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IGameplayState
    {
        private readonly Game _game;
        private readonly IInputProvider _inputProvider;
        private readonly ICardDatabase _cardDatabase;

        // --- Sub-Systems (Separated Concerns) ---
        internal GameplayView _view;

        // Changed to internal so Tests can initialize it manually
        internal MatchController _matchController;

        // --- Context & Systems ---
        internal MatchContext _matchContext;

        internal InputManager _inputManagerBacking;
        internal IUISystem _uiManagerBacking;

        // --- State Variables ---
        internal bool _isMarketOpenBacking = false;

        // --- Properties (IGameplayState Implementation) ---
        public InputManager InputManager => _inputManagerBacking;
        public IUISystem UIManager => _uiManagerBacking;

        public IMapManager MapManager => _matchContext?.MapManager;
        public IMarketManager MarketManager => _matchContext?.MarketManager;
        public IActionSystem ActionSystem => _matchContext?.ActionSystem;
        public TurnManager TurnManager => _matchContext?.TurnManager as TurnManager;
        public MatchContext MatchContext => _matchContext;

        public IInputMode InputMode { get; set; }

        // Safe navigation for View properties
        public int HandY => _view?.HandY ?? 0;
        public int PlayedY => _view?.PlayedY ?? 0;

        public bool IsMarketOpen
        {
            get => _isMarketOpenBacking;
            set
            {
                _isMarketOpenBacking = value;
                if (_isMarketOpenBacking)
                {
                    InputMode = new MarketInputMode(this, _inputManagerBacking, _matchContext);
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

            GameLogger.Initialize();

            // 1. Initialize Managers
            _inputManagerBacking = new InputManager(_inputProvider);
            _uiManagerBacking = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            // 2. Initialize Logic & View
            _view = new GameplayView(graphicsDevice);
            _view.LoadContent(_game.Content);

            // 3. Build World
            var builder = new WorldBuilder(_cardDatabase, "data/map.json");
            var worldData = builder.Build();

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase
            );

            // 4. Initialize Controller
            _matchController = new MatchController(_matchContext);

            _matchContext.ActionSystem.SetCurrentPlayer(_matchContext.ActivePlayer);

            if (_matchContext.TurnManager.Players != null)
            {
                foreach (var player in _matchContext.TurnManager.Players)
                {
                    player.DrawCards(5);
                }
            }

            _matchContext.MapManager.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            InitializeEventSubscriptions();
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

            // Safe navigation: Don't crash if View is null (Headless/Test mode)
            _view?.Update(_matchContext, _inputManagerBacking, IsMarketOpen);

            IGameCommand command = InputMode.HandleInput(
                _inputManagerBacking,
                _matchContext.MarketManager,
                _matchContext.MapManager,
                _matchContext.ActivePlayer,
                _matchContext.ActionSystem);

            command?.Execute(this);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_game == null) return;

            string targetingText = "";
            if (_matchContext.ActionSystem.IsTargeting())
            {
                targetingText = GetTargetingText(_matchContext.ActionSystem.CurrentState);
            }

            // Safe navigation
            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, (UIManager)_uiManagerBacking, IsMarketOpen, targetingText);
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
            _matchController.PlayCard(card);
        }

        public void ResolveCardEffects(Card card) => _matchController.ResolveCardEffects(card);
        public void MoveCardToPlayed(Card card) => _matchController.MoveCardToPlayed(card);

        public void EndTurn()
        {
            if (_matchContext.ActionSystem.IsTargeting()) _matchContext.ActionSystem.CancelTargeting();
            _matchController.EndTurn();
            SwitchToNormalMode();
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

        private bool HandleGlobalInput()
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

        private void HandleSpySelectionInput()
        {
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;
            if (_view == null) return; // Cannot handle visual clicks without View

            var site = _matchContext.ActionSystem.PendingSite;
            PlayerColor? clickedSpy = _view.GetClickedSpyReturnButton(
                _inputManagerBacking.MousePosition.ToPoint(),
                site,
                _uiManagerBacking.ScreenWidth);

            if (clickedSpy.HasValue)
            {
                _matchContext.ActionSystem.FinalizeSpyReturn(clickedSpy.Value);
            }
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
                _matchController.ResolveCardEffects(_matchContext.ActionSystem.PendingCard);
                _matchController.MoveCardToPlayed(_matchContext.ActionSystem.PendingCard);
            }
            _matchContext.ActionSystem.CancelTargeting();
            SwitchToNormalMode();
        }

        // Safe navigation for View methods
        public Card GetHoveredHandCard() => _view?.GetHoveredHandCard();
        public Card GetHoveredMarketCard() => _view?.GetHoveredMarketCard();
    }
}