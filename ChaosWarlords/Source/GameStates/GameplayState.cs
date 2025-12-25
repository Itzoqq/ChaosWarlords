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
        internal IMatchManager _matchManager;
        internal MatchContext _matchContext;
        internal InputManager _inputManagerBacking;
        internal IUIManager _uiManagerBacking;
        internal bool _isMarketOpenBacking = false;

        // New Coordinators
        private GameplayInputCoordinator _inputCoordinator;
        private InteractionMapper _interactionMapper;
        private CardPlaySystem _cardPlaySystem;

        public InputManager InputManager => _inputManagerBacking;
        public IUIManager UIManager => _uiManagerBacking;
        public IMatchManager MatchManager => _matchManager;

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

            var builder = new TestWorldFactory(_cardDatabase, "data/map.json");
            var worldData = builder.Build();

            _matchContext = new MatchContext(
                worldData.TurnManager,
                worldData.MapManager,
                worldData.MarketManager,
                worldData.ActionSystem,
                _cardDatabase
            );

            _matchManager = new MatchManager(_matchContext);

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
            
            // 3. Initialize CardPlaySystem
            _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, () => SwitchToTargetingMode());
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

            if (_isConfirmationPopupOpen) return; // Block other input if popup is open

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

            _view?.Draw(spriteBatch, _matchContext, _inputManagerBacking, (UIManager)_uiManagerBacking, IsMarketOpen, targetingText, IsConfirmationPopupOpen, IsPauseMenuOpen);
        }

        public void PlayCard(Card card)
        {
            _cardPlaySystem.PlayCard(card);
        }

        public bool HasViableTargets(Card card) => _cardPlaySystem.HasViableTargets(card);



        public void MoveCardToPlayed(Card card) => _matchManager.MoveCardToPlayed(card);

        public bool CanEndTurn(out string reason) => _matchManager.CanEndTurn(out reason);

        public void EndTurn()
        {
            if (_matchContext.ActionSystem.IsTargeting()) _matchContext.ActionSystem.CancelTargeting();
            _matchManager.EndTurn();
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
                if (_isPauseMenuOpen)
                {
                    _isPauseMenuOpen = false;
                }
                else
                {
                    _isPauseMenuOpen = true; 
                    if (IsMarketOpen) IsMarketOpen = false;
                    _matchContext.ActionSystem.CancelTargeting();
                    SwitchToNormalMode();
                    if (_isConfirmationPopupOpen) _isConfirmationPopupOpen = false;
                }
                return true;
            }

            if (_isPauseMenuOpen) return true; // Block input when paused

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
                            GameLogger.Log("No valid cards to promote. Promotion effects skipped.", LogChannel.Info);
                            HandleEndTurnRequest(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        HandleEndTurnRequest(this, EventArgs.Empty);
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



        public string GetTargetingText(ActionState state) => state.ToString();

        internal void InitializeEventSubscriptions()
        {
            _uiManagerBacking.OnMarketToggleRequest -= HandleMarketToggle;
            _uiManagerBacking.OnAssassinateRequest -= HandleAssassinateRequest;
            _uiManagerBacking.OnReturnSpyRequest -= HandleReturnSpyRequest;
            _uiManagerBacking.OnEndTurnRequest -= HandleEndTurnRequest;
            _uiManagerBacking.OnPopupConfirm -= HandlePopupConfirm;
            _uiManagerBacking.OnPopupCancel -= HandlePopupCancel;

            _matchContext.ActionSystem.OnActionCompleted -= HandleActionCompleted;
            _matchContext.ActionSystem.OnActionFailed -= HandleActionFailed;

            _uiManagerBacking.OnMarketToggleRequest += HandleMarketToggle;
            _uiManagerBacking.OnAssassinateRequest += HandleAssassinateRequest;
            _uiManagerBacking.OnReturnSpyRequest += HandleReturnSpyRequest;
            _uiManagerBacking.OnEndTurnRequest += HandleEndTurnRequest;
            _uiManagerBacking.OnPopupConfirm += HandlePopupConfirm;
            _uiManagerBacking.OnPopupCancel += HandlePopupCancel;

            _uiManagerBacking.OnResumeRequest += HandleResumeRequest;
            _uiManagerBacking.OnMainMenuRequest += HandleMainMenuRequest;
            _uiManagerBacking.OnExitRequest += HandleExitRequest; // Exit from Pause Menu

            _matchContext.ActionSystem.OnActionCompleted += HandleActionCompleted;
            _matchContext.ActionSystem.OnActionFailed += HandleActionFailed;
        }

        private void HandleMarketToggle(object sender, EventArgs e) => ToggleMarket();
        private void HandleAssassinateRequest(object sender, EventArgs e) { _matchContext.ActionSystem.TryStartAssassinate(); if (_matchContext.ActionSystem.IsTargeting()) SwitchToTargetingMode(); }
        private void HandleReturnSpyRequest(object sender, EventArgs e) { _matchContext.ActionSystem.TryStartReturnSpy(); if (_matchContext.ActionSystem.IsTargeting()) SwitchToTargetingMode(); }
        
        // End Turn Logic
        private bool _isConfirmationPopupOpen = false;
        public bool IsConfirmationPopupOpen => _isConfirmationPopupOpen;

        // Pause Menu Logic
        private bool _isPauseMenuOpen = false;
        public bool IsPauseMenuOpen => _isPauseMenuOpen;

        private void HandleEndTurnRequest(object sender, EventArgs e)
        {
            GameLogger.Log("Gameplay: EndTurn Request Received", LogChannel.Info);
            bool hasUnplayedCards = _matchContext.ActivePlayer.Hand.Count > 0;
            if (hasUnplayedCards)
            {
                GameLogger.Log("Gameplay: Opening Confirmation Popup", LogChannel.Info);
                _isConfirmationPopupOpen = true;
            }
            else
            {
                GameLogger.Log("Gameplay: Ending Turn Immediately", LogChannel.Info);
                EndTurn();
            }
        }

        private void HandlePopupConfirm(object sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                GameLogger.Log("Gameplay: Popup Confirmed - Ending Turn", LogChannel.Info);
                _isConfirmationPopupOpen = false;
                EndTurn();
            }
        }

        private void HandlePopupCancel(object sender, EventArgs e)
        {
            if (_isConfirmationPopupOpen)
            {
                GameLogger.Log("Gameplay: Popup Cancelled", LogChannel.Info);
                _isConfirmationPopupOpen = false;
            }
        }

        // --- PAUSE MENU HANDLERS ---
        private void HandleResumeRequest(object sender, EventArgs e)
        {
            if (_isPauseMenuOpen) _isPauseMenuOpen = false;
        }

        private void HandleMainMenuRequest(object sender, EventArgs e)
        {
            if (_isPauseMenuOpen)
            {
                // Navigate to Main Menu
                // We access StateManager via Game1 usually, but here we only have Game. 
                // We can cast `_game` if it's Game1, or we need to pass StateManager in constructor.
                // Assuming Game1 is the type since it was used in MainMenuState.
                if (_game is Game1 g1)
                {
                    g1.StateManager.ChangeState(new MainMenuState(g1));
                }
            }
        }

        private void HandleExitRequest(object sender, EventArgs e)
        {
            if (_isPauseMenuOpen)
            {
                _game.Exit();
            }
        }

        private void HandleActionFailed(object sender, string msg) => GameLogger.Log(msg, LogChannel.Error);

        private void HandleActionCompleted(object sender, EventArgs e)
        {
            if (_matchContext.ActionSystem.PendingCard != null)
            {
                _matchManager.PlayCard(_matchContext.ActionSystem.PendingCard);
            }
            _matchContext.ActionSystem.CancelTargeting();
            SwitchToNormalMode();
        }

        public Card GetHoveredHandCard() => _interactionMapper.GetHoveredHandCard();
        public Card GetHoveredPlayedCard() => _interactionMapper.GetHoveredPlayedCard(_inputManagerBacking);
        public Card GetHoveredMarketCard() => _interactionMapper.GetHoveredMarketCard();
    }
}