using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.States.Input;
using System;
using System.Linq;

namespace ChaosWarlords.Source.States
{
    public class GameplayState : IGameplayState
    {
        private readonly Game _game;
        private readonly IInputProvider _inputProvider;
        private readonly ICardDatabase _cardDatabase;

        internal GameplayView _view;
        internal MatchController _matchController;
        internal MatchContext _matchContext;
        internal InputManager _inputManagerBacking;
        internal IUISystem _uiManagerBacking;
        internal bool _isMarketOpenBacking = false;

        public InputManager InputManager => _inputManagerBacking;
        public IUISystem UIManager => _uiManagerBacking;

        public IMapManager MapManager => _matchContext?.MapManager;
        public IMarketManager MarketManager => _matchContext?.MarketManager;
        public IActionSystem ActionSystem => _matchContext?.ActionSystem;
        public ITurnManager TurnManager => _matchContext?.TurnManager;
        public MatchContext MatchContext => _matchContext;

        public IInputMode InputMode { get; set; }

        public int HandY => _view?.HandY ?? 0;
        public int PlayedY => _view?.PlayedY ?? 0;

        public bool IsMarketOpen
        {
            get => _isMarketOpenBacking;
            set
            {
                _isMarketOpenBacking = value;
                if (_isMarketOpenBacking)
                    InputMode = new MarketInputMode(this, _inputManagerBacking, _matchContext);
                else
                    SwitchToNormalMode();
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

            _inputManagerBacking = new InputManager(_inputProvider);
            _uiManagerBacking = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            _view = new GameplayView(graphicsDevice);
            _view.LoadContent(_game.Content);

            var builder = new WorldBuilder(_cardDatabase, "data/map.json");
            var worldData = builder.Build();

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase
            );

            _matchController = new MatchController(_matchContext);

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

            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, (UIManager)_uiManagerBacking, IsMarketOpen, targetingText);
        }

        public void PlayCard(Card card)
        {
            // 1. Check for Targeting Effects
            foreach (var effect in card.Effects)
            {
                // Note: Promote is NOT a targeting effect here; it adds credit for End of Turn.
                if (IsTargetingEffect(effect.Type))
                {
                    // If we have valid targets, we pause play and enter targeting mode
                    if (HasValidTargets(effect.Type))
                    {
                        _matchContext.ActionSystem.StartTargeting(GetTargetingState(effect.Type), card);
                        SwitchToTargetingMode();
                        return; // Exit here. MatchController.PlayCard will be called in HandleActionCompleted
                    }
                    else
                    {
                        // 2. Whiff Logic
                        // If no targets exist, the rules say we skip the impossible instruction
                        // but CONTINUE playing the card (for resources/focus).
                        GameLogger.Log($"No valid targets for {effect.Type}. Effect skipped.", LogChannel.Info);
                    }
                }
            }

            // 3. Fallthrough
            // If no targeting was needed (or all targeting whiffed), play immediately.
            _matchController.PlayCard(card);
        }

        private bool HasValidTargets(EffectType type)
        {
            var p = _matchContext.ActivePlayer;
            var map = _matchContext.MapManager;

            return type switch
            {
                EffectType.Assassinate => map.HasValidAssassinationTarget(p),
                EffectType.Supplant => map.HasValidAssassinationTarget(p), // Usually similar to assassinate
                EffectType.ReturnUnit => map.HasValidReturnSpyTarget(p),
                EffectType.PlaceSpy => map.HasValidPlaceSpyTarget(p),
                // Promote Removed: Promotion happens at end of turn via credits
                _ => true
            };
        }

        // We delegate resolution entirely to MatchController now.
        // We only keep this if other systems call it, otherwise it can be removed.
        // For now, we wrap it to ensure compatibility.
        public void ResolveCardEffects(Card card)
        {
            // If we ever need to manually resolve without playing (rare), we need the snapshot logic.
            // Ideally, avoid calling this directly; use MatchController.PlayCard.
            // If legacy code calls this, we default to no focus or perform a calculation:

            // Replicate basic focus check just for safety if this method is called standalone
            int playedCount = _matchContext.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);
            bool focus = playedCount > 0 || _matchContext.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);

            _matchController.ResolveCardEffects(card, focus);
        }

        public void MoveCardToPlayed(Card card) => _matchController.MoveCardToPlayed(card);

        public bool CanEndTurn(out string reason) => _matchController.CanEndTurn(out reason);

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
            if (_matchContext.ActionSystem.CurrentState == ActionState.SelectingCardToPromote)
            {
                int amount = 1;
                var promoteEffect = _matchContext.ActionSystem.PendingCard?.Effects.FirstOrDefault(e => e.Type == EffectType.Promote);
                if (promoteEffect != null) amount = promoteEffect.Amount;

                // If this was triggered by End Turn pending promotions, override amount
                if (_matchContext.TurnManager.CurrentTurnContext.PendingPromotionsCount > 0)
                {
                    amount = _matchContext.TurnManager.CurrentTurnContext.PendingPromotionsCount;
                }

                InputMode = new PromoteInputMode(
                    this,
                    _inputManagerBacking,
                    _matchContext.ActionSystem,
                    amount
                );
            }
            else
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
            if (_inputManagerBacking.IsKeyJustPressed(Keys.Escape))
            {
                _game.Exit();
                return true;
            }

            if (_inputManagerBacking.IsKeyJustPressed(Keys.Enter))
            {
                // Check if we can end turn (MatchController checks constraints)
                if (CanEndTurn(out string reason))
                {
                    // Enforce Promotion at End of Turn
                    int pending = _matchContext.TurnManager.CurrentTurnContext.PendingPromotionsCount;
                    if (pending > 0)
                    {
                        GameLogger.Log($"You must promote {pending} card(s) before ending your turn.", LogChannel.Warning);
                        SwitchToPromoteMode(pending);
                    }
                    else
                    {
                        EndTurn();
                    }
                }
                else
                {
                    GameLogger.Log(reason, LogChannel.Warning);
                }
                return true;
            }

            if (_inputManagerBacking.IsRightMouseJustClicked())
            {
                if (IsMarketOpen) { IsMarketOpen = false; return true; }
                if (_matchContext.ActionSystem.IsTargeting()) { _matchContext.ActionSystem.CancelTargeting(); SwitchToNormalMode(); return true; }
            }
            return false;
        }

        public void SwitchToPromoteMode(int amount)
        {
            _matchContext.ActionSystem.StartTargeting(ActionState.SelectingCardToPromote);
            InputMode = new PromoteInputMode(
                this,
                _inputManagerBacking,
                _matchContext.ActionSystem,
                amount
            );
        }

        private void HandleSpySelectionInput()
        {
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;
            if (_view == null) return;

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
            // Removed Promote from here. It is handled via Credits and EndTurn checks.
            return type == EffectType.Assassinate ||
                   type == EffectType.ReturnUnit ||
                   type == EffectType.Supplant ||
                   type == EffectType.PlaceSpy;
        }

        private ActionState GetTargetingState(EffectType type)
        {
            return type switch
            {
                EffectType.Assassinate => ActionState.TargetingAssassinate,
                EffectType.ReturnUnit => ActionState.TargetingReturn,
                EffectType.Supplant => ActionState.TargetingSupplant,
                EffectType.PlaceSpy => ActionState.TargetingPlaceSpy,
                // Promote => ActionState.SelectingCardToPromote, // Handled manually at EndTurn
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
                // Action complete, now actually "Play" the card to get resources/Focus
                _matchController.PlayCard(_matchContext.ActionSystem.PendingCard);
            }
            _matchContext.ActionSystem.CancelTargeting();
            SwitchToNormalMode();
        }

        public Card GetHoveredHandCard() => _view?.GetHoveredHandCard();
        public Card GetHoveredPlayedCard() => _view?.GetHoveredPlayedCard(_matchContext, _inputManagerBacking);
        public Card GetHoveredMarketCard() => _view?.GetHoveredMarketCard();
    }
}