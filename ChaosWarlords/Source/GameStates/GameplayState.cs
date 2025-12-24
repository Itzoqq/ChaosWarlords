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
        internal IMatchController _matchController;
        internal MatchContext _matchContext;
        internal InputManager _inputManagerBacking;
        internal IUISystem _uiManagerBacking;
        internal bool _isMarketOpenBacking = false;

        // New Coordinators
        private GameplayInputCoordinator _inputCoordinator;
        private InteractionMapper _interactionMapper;

        public InputManager InputManager => _inputManagerBacking;
        public IUISystem UIManager => _uiManagerBacking;
        public IMatchController MatchController => _matchController;

        public IMapManager MapManager => _matchContext?.MapManager;
        public IMarketManager MarketManager => _matchContext?.MarketManager;
        public IActionSystem ActionSystem => _matchContext?.ActionSystem;
        public ITurnManager TurnManager => _matchContext?.TurnManager;
        public MatchContext MatchContext => _matchContext;

        public IInputMode InputMode => _inputCoordinator.CurrentMode;

        public int HandY => _view?.HandY ?? 0;
        public int PlayedY => _view?.PlayedY ?? 0;

        public bool IsMarketOpen
        {
            get => _isMarketOpenBacking;
            set
            {
                _isMarketOpenBacking = value;
                _inputCoordinator.SetMarketMode(value);
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

            // 1. Initialize InteractionMapper
            _interactionMapper = new InteractionMapper(_view);

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

            // 2. Initialize InputCoordinator
            _inputCoordinator = new GameplayInputCoordinator(this, _inputManagerBacking, _matchContext);
        }

        public void UnloadContent()
        {
            if (_uiManagerBacking != null)
            {
                _uiManagerBacking.OnMarketToggleRequest -= HandleMarketToggle;
                _uiManagerBacking.OnAssassinateRequest -= HandleAssassinateRequest;
                _uiManagerBacking.OnReturnSpyRequest -= HandleReturnSpyRequest;
            }

            if (_matchContext?.ActionSystem != null)
            {
                _matchContext.ActionSystem.OnActionCompleted -= HandleActionCompleted;
                _matchContext.ActionSystem.OnActionFailed -= HandleActionFailed;
            }
        }

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

            // 3. Delegate Input to Coordinator
            _inputCoordinator.HandleInput();
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
            bool enteredTargeting = false;

            // 1. Check for Targeting Effects
            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    if (HasValidTargets(effect.Type))
                    {
                        _matchContext.ActionSystem.StartTargeting(GetTargetingState(effect.Type), card);
                        SwitchToTargetingMode();
                        enteredTargeting = true;
                        break; // Stop checking other effects once we enter targeting
                    }
                    else
                    {
                        // SKIPPING TARGETING:
                        // If no targets exist, we define this as "Targeting phase complete/skipped"
                        // and proceed to play the card data.
                        GameLogger.Log($"Skipping targeting for {card.Name}: No valid targets for {effect.Type}.", LogChannel.Info);
                    }
                }
            }

            // 2. Play immediately if no targeting was started
            if (!enteredTargeting)
            {
                _matchController.PlayCard(card);
            }
        }

        public bool HasViableTargets(Card card)
        {
            if (card == null) return false;
            bool hasTargeting = card.Effects.Any(e => IsTargetingEffect(e.Type));
            if (!hasTargeting) return true;

            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    if (HasValidTargets(effect.Type)) return true;
                }
            }
            return false;
        }

        private bool HasValidTargets(EffectType type)
        {
            var p = _matchContext.ActivePlayer;
            var map = _matchContext.MapManager;

            return type switch
            {
                EffectType.Assassinate => map.HasValidAssassinationTarget(p),
                EffectType.Supplant => map.HasValidAssassinationTarget(p),
                EffectType.ReturnUnit => map.HasValidReturnSpyTarget(p) || map.HasValidReturnTroopTarget(p),
                EffectType.PlaceSpy => map.HasValidPlaceSpyTarget(p),
                EffectType.MoveUnit => map.HasValidMoveSource(p),
                EffectType.Devour => p.Hand.Count > 0,
                _ => true
            };
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

        public void SwitchToTargetingMode() => _inputCoordinator.SwitchToTargetingMode();
        public void SwitchToNormalMode() => _inputCoordinator.SwitchToNormalMode();

        // --- FIX IS HERE ---
        public void SwitchToPromoteMode(int amount)
        {
            // We must set the ActionSystem state explicitly so the InputCoordinator knows 
            // to instantiate the PromoteInputMode instead of the generic TargetingInputMode.
            // We pass 'null' for the card because Promotion is a turn-phase action, not a card-specific action.
            _matchContext.ActionSystem.StartTargeting(ActionState.SelectingCardToPromote, null);

            _inputCoordinator.SwitchToTargetingMode();
        }
        // -------------------

        private bool HandleGlobalInput()
        {
            if (_inputManagerBacking.IsKeyJustPressed(Keys.Escape))
            {
                _game.Exit();
                return true;
            }

            if (_inputManagerBacking.IsKeyJustPressed(Keys.Enter))
            {
                if (CanEndTurn(out string reason))
                {
                    int pending = _matchContext.TurnManager.CurrentTurnContext.PendingPromotionsCount;
                    if (pending > 0)
                    {
                        // Strict Rule Check
                        // Only enter Promote Mode if there are actually cards we CAN promote.
                        var activePlayer = _matchContext.TurnManager.ActivePlayer;
                        bool hasValidTargets = activePlayer.PlayedCards.Any(c =>
                            _matchContext.TurnManager.CurrentTurnContext.HasValidCreditFor(c));

                        if (hasValidTargets)
                        {
                            GameLogger.Log($"You must promote {pending} card(s) before ending your turn.", LogChannel.Warning);
                            SwitchToPromoteMode(pending);
                        }
                        else
                        {
                            GameLogger.Log("No valid cards to promote. Promotion effects skipped.", LogChannel.Info);
                            EndTurn();
                        }
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

        private void HandleSpySelectionInput()
        {
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;
            if (_view == null) return;

            var site = _matchContext.ActionSystem.PendingSite;

            PlayerColor? clickedSpy = _interactionMapper.GetClickedSpyReturnButton(
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
            return type == EffectType.Assassinate ||
                   type == EffectType.ReturnUnit ||
                   type == EffectType.Supplant ||
                   type == EffectType.PlaceSpy ||
                   type == EffectType.MoveUnit ||
                   type == EffectType.Devour;
        }

        private ActionState GetTargetingState(EffectType type)
        {
            return type switch
            {
                EffectType.Assassinate => ActionState.TargetingAssassinate,
                EffectType.ReturnUnit => ActionState.TargetingReturn,
                EffectType.Supplant => ActionState.TargetingSupplant,
                EffectType.PlaceSpy => ActionState.TargetingPlaceSpy,
                EffectType.MoveUnit => ActionState.TargetingMoveSource,
                EffectType.Devour => ActionState.TargetingDevourHand,
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
                _matchController.PlayCard(_matchContext.ActionSystem.PendingCard);
            }
            _matchContext.ActionSystem.CancelTargeting();
            SwitchToNormalMode();
        }

        public Card GetHoveredHandCard() => _interactionMapper.GetHoveredHandCard();
        public Card GetHoveredPlayedCard() => _interactionMapper.GetHoveredPlayedCard(_inputManagerBacking);
        public Card GetHoveredMarketCard() => _interactionMapper.GetHoveredMarketCard();
    }
}