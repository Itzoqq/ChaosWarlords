using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Contexts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Reflection;
using ChaosWarlords.Source.Entities.Cards; // For CardBuilder
using ChaosWarlords.Tests; // For TestData
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Composition; // For IGameDependencies
using System.Collections.Generic; // For List<Card>

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    public class UIEventMediatorTests
    {
        private IGameplayState _gameState = null!;
        private IUIManager _uiManager = null!;
        private IActionSystem _actionSystem = null!;
        private IGameLogger _logger = null!;
        private UIEventMediator _mediator = null!;
        private IMarketStateManager _marketStateManager = null!;
        
        // Deep Mocks for MatchContext
        private ITurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IPlayerStateManager _playerStateManager = null!;
        private MatchContext _matchContext = null!;
        private ICardDatabase _cardDatabase = null!;
        private IMatchManager _matchManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _gameState = Substitute.For<IGameplayState>();
            _uiManager = Substitute.For<IUIManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _logger = Substitute.For<IGameLogger>();
            _marketStateManager = Substitute.For<IMarketStateManager>();
            
            // Setup MatchContext Dependencies
            _turnManager = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();
            _cardDatabase = Substitute.For<ICardDatabase>();
            _matchManager = Substitute.For<IMatchManager>();

            // Create Real MatchContext with Mocks
            // Note: MatchContext ctor logic is simple assignment
            _matchContext = new MatchContext(
                turn: _turnManager,
                map: _mapManager,
                market: _marketManager,
                action: _actionSystem,
                cardDb: _cardDatabase,
                playerState: _playerStateManager,
                uiMediator: null,
                logger: _logger,
                seed: 12345
            );

            // Mock GameState properties
            _gameState.MarketStateManager.Returns(_marketStateManager);
            _gameState.MatchContext.Returns(_matchContext);
            _gameState.MatchManager.Returns(_matchManager);

            // Construct Mediator
            _mediator = new UIEventMediator(_gameState, _uiManager, _actionSystem, _logger, game: null);
            _mediator.Initialize();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mediator.Cleanup();
        }

        // --- Escape Key Priority Tests (Preserved) ---

        [TestMethod]
        public void HandleEscapeKeyPress_Priority1_CancelsTargeting_IfActive()
        {
            _actionSystem.IsTargeting().Returns(true);
            _gameState.IsMarketOpen.Returns(true);
            _marketStateManager.IsOpen.Returns(true);

            _mediator.HandleEscapeKeyPress();

            _actionSystem.Received(1).CancelTargeting();
            _gameState.Received(1).SwitchToNormalMode();
            _marketStateManager.DidNotReceive().Close();
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_Priority2_ClosesMarket_IfOpen()
        {
            _actionSystem.IsTargeting().Returns(false);
            _gameState.IsMarketOpen.Returns(true);
            _marketStateManager.IsOpen.Returns(true);

            _mediator.HandleEscapeKeyPress();

            _marketStateManager.Received(1).Close();
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_Priority3_ClosesConfirmationPopup_IfOpen()
        {
            _actionSystem.IsTargeting().Returns(false);
            _gameState.IsMarketOpen.Returns(false);

            SetPrivateField(_mediator, "_isConfirmationPopupOpen", true);
            Assert.IsTrue(_mediator.IsConfirmationPopupOpen, "Setup failed to set popup open");

            _mediator.HandleEscapeKeyPress();

            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_Priority4_DeclinesOptionalEffect_IfOpen()
        {
            _actionSystem.IsTargeting().Returns(false);
            _gameState.IsMarketOpen.Returns(false);

            bool declineCalled = false;
            _mediator.RequestOptionalEffect(
                null!, 
                null!, 
                () => { }, 
                () => { declineCalled = true; });

            Assert.IsTrue(_mediator.IsOptionalEffectPopupOpen, "Setup failed to set optional popup open");

            _mediator.HandleEscapeKeyPress();

            Assert.IsTrue(declineCalled, "Decline callback should be invoked");
            Assert.IsFalse(_mediator.IsOptionalEffectPopupOpen);
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_Default_OpensPauseMenu_WhenNoOtherState()
        {
            _actionSystem.IsTargeting().Returns(false);
            _gameState.IsMarketOpen.Returns(false);

            _mediator.HandleEscapeKeyPress();

            Assert.IsTrue(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_ClosesPauseMenu_IfAlreadyOpen()
        {
            SetPrivateField(_mediator, "_isPauseMenuOpen", true);
            _actionSystem.IsTargeting().Returns(true); // Should be ignored

            _mediator.HandleEscapeKeyPress();

            Assert.IsFalse(_mediator.IsPauseMenuOpen);
            _actionSystem.DidNotReceive().CancelTargeting();
        }

        // --- Restored Functionality Tests ---

        [TestMethod]
        public void HandleMarketToggle_WhenOpen_ClosesMarket()
        {
            // Arrange
            _marketStateManager.IsOpen.Returns(true);

            // Act
            _uiManager.OnMarketToggleRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            _marketStateManager.Received(1).Close();
        }

        [TestMethod]
        public void HandleMarketToggle_WhenClosed_OpensMarket()
        {
            // Arrange
            _marketStateManager.IsOpen.Returns(false);

            // Act
            _uiManager.OnMarketToggleRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            _marketStateManager.Received(1).OpenForBrowsing();
        }

        [TestMethod]
        public void HandleAssassinateRequest_TryStartsAndSwitchesMode()
        {
            // Arrange
            _actionSystem.IsTargeting().Returns(true); // Assume start successful

            // Act
            _uiManager.OnAssassinateRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            _actionSystem.Received(1).TryStartAssassinate();
            _gameState.Received(1).SwitchToTargetingMode();
        }

        [TestMethod]
        public void HandleReturnSpyRequest_TryStartsAndSwitchesMode()
        {
            // Arrange
            _actionSystem.IsTargeting().Returns(true); // Assume start successful

            // Act
            _uiManager.OnReturnSpyRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            _actionSystem.Received(1).TryStartReturnSpy();
            _gameState.Received(1).SwitchToTargetingMode();
        }

        [TestMethod]
        public void HandleEndTurnRequest_WithUnplayedCards_OpensConfirmationPopup()
        {
            // Arrange
            _gameState.CanEndTurn(out _).Returns(true);
            
            // Setup Hand with Cards
            var player = new Player(PlayerColor.Red);
            player.AddToHand(new CardBuilder().WithName("Test").WithCost(1).WithAspect(CardAspect.Warlord).Build());
            _turnManager.ActivePlayer.Returns(player);

            // Act
            _uiManager.OnEndTurnRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            Assert.IsTrue(_mediator.IsConfirmationPopupOpen);
            // Should NOT execute end turn command yet
            _gameState.DidNotReceive().RecordAndExecuteCommand(Arg.Any<EndTurnCommand>());
        }

        [TestMethod]
        public void HandleEndTurnRequest_WithoutUnplayedCards_ExecutesEndTurn()
        {
            // Arrange
            _gameState.CanEndTurn(out _).Returns(true);
            
            // Setup Hand Empty
            var player = new Player(PlayerColor.Red);
            // Hand is empty by default
            _turnManager.ActivePlayer.Returns(player);

            // Mock TurnContext for promotion check
            var turnContext = new TurnContext(player, _logger);
            // turnContext.PendingPromotionsCount is 0 by default
            _turnManager.CurrentTurnContext.Returns(turnContext);

            // Act
            _uiManager.OnEndTurnRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            _gameState.Received(1).RecordAndExecuteCommand(Arg.Any<EndTurnCommand>());
        }

        [TestMethod]
        public void HandlePopupConfirm_EndsTurn()
        {
            // Arrange: Open popup first
            SetPrivateField(_mediator, "_isConfirmationPopupOpen", true);

            // Logic calls HandleEndTurnWithPromotionCheck checks promotions
            var player = new Player(PlayerColor.Red);
            _turnManager.ActivePlayer.Returns(player);
            var turnContext = new TurnContext(player, _logger);
            // turnContext.PendingPromotionsCount is 0 by default
            _turnManager.CurrentTurnContext.Returns(turnContext);

            // Act
            _uiManager.OnPopupConfirm += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            _gameState.Received(1).RecordAndExecuteCommand(Arg.Any<EndTurnCommand>());
        }

        [TestMethod]
        public void HandlePopupCancel_ClosesPopup()
        {
            // Arrange
            SetPrivateField(_mediator, "_isConfirmationPopupOpen", true);

            // Act
            _uiManager.OnPopupCancel += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            _gameState.DidNotReceive().RecordAndExecuteCommand(Arg.Any<EndTurnCommand>());
        }

        [TestMethod]
        public void HandleResumeRequest_ClosesPauseMenu()
        {
            // Arrange
            SetPrivateField(_mediator, "_isPauseMenuOpen", true);

            // Act
            _uiManager.OnResumeRequest += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleActionCompleted_PlaysPendingCard_AndResetsMode()
        {
            // Arrange
            var card = new CardBuilder().WithName("Test").WithCost(1).WithAspect(CardAspect.Warlord).Build();
            _actionSystem.PendingCard.Returns(card);
            _gameState.IsMarketOpen.Returns(false); // Should switch to normal

            // Act
            _actionSystem.OnActionCompleted += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            _matchManager.Received(1).PlayCard(card);
            _gameState.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void HandleActionCompleted_DoesNotResetMode_IfMarketOpen()
        {
            // Arrange
            var card = new CardBuilder().WithName("Test").WithCost(1).WithAspect(CardAspect.Warlord).Build();
            _actionSystem.PendingCard.Returns(card);
            _gameState.IsMarketOpen.Returns(true); // Should Stay in Market Mode

            // Act
            _actionSystem.OnActionCompleted += Raise.Event<EventHandler>(this, EventArgs.Empty);

            // Assert
            _matchManager.Received(1).PlayCard(card);
            _gameState.DidNotReceive().SwitchToNormalMode();
        }
        
        [TestMethod]
        public void RequestOptionalEffect_CallbacksWorkViaPopupEvents()
        {
             // Arrange
            bool acceptCalled = false;
            bool declineCalled = false;

            _mediator.RequestOptionalEffect(null!, null!, () => acceptCalled = true, () => declineCalled = true);
            
            // Test Accept via Popup Confirm
            _uiManager.OnPopupConfirm += Raise.Event<EventHandler>(this, EventArgs.Empty);
            Assert.IsTrue(acceptCalled);
            Assert.IsFalse(_mediator.IsOptionalEffectPopupOpen);

            // Reset
            acceptCalled = false;
            _mediator.RequestOptionalEffect(null!, null!, () => acceptCalled = true, () => declineCalled = true);

            // Test Decline via Popup Cancel
            _uiManager.OnPopupCancel += Raise.Event<EventHandler>(this, EventArgs.Empty);
            Assert.IsTrue(declineCalled);
            Assert.IsFalse(_mediator.IsOptionalEffectPopupOpen);
        }

        [TestMethod]
        public void OnInteractionRequested_RaisesOptionalEffectEvent_AndRoutesResponseBack()
        {
            // Arrange: ActionSystem never calls IUIEventMediator directly anymore - it raises
            // OnInteractionRequested, and UIEventMediator.HandleInteractionRequest (subscribed
            // in Initialize(), above) is what's expected to translate that into the same
            // OnOptionalEffectRequested popup event RequestOptionalEffect always raised.
            var card = new CardBuilder().WithName("Test").WithCost(1).WithAspect(CardAspect.Warlord).Build();
            var effect = new CardEffect(EffectType.Devour, 1) { IsOptional = true };
            var effectContext = new EffectContext(
                ActionState.TargetingDevourHand,
                card,
                requiresInput: true,
                description: "test optional effect",
                onResolved: _ => { },
                sourceEffect: effect);

            bool? response = null;
            var request = new InteractionRequest(effectContext, accepted => response = accepted);

            Card? capturedCard = null;
            CardEffect? capturedEffect = null;
            Action? capturedAccept = null;
            Action? capturedDecline = null;
            _mediator.OnOptionalEffectRequested += (c, e, accept, decline) =>
            {
                capturedCard = c;
                capturedEffect = e;
                capturedAccept = accept;
                capturedDecline = decline;
            };

            // Act
            _actionSystem.OnInteractionRequested += Raise.Event<Action<InteractionRequest>>(request);

            // Assert: the popup event fired with the request's card/effect...
            Assert.AreEqual(card, capturedCard);
            Assert.AreEqual(effect, capturedEffect);
            Assert.IsNotNull(capturedAccept);
            Assert.IsNotNull(capturedDecline);

            // ...and invoking the popup's accept action calls back into request.OnResponse.
            capturedAccept!.Invoke();
            Assert.IsTrue(response);
        }

        [TestMethod]
        public void Update_SetsConfirmationPopupVisible_OnlyForConfirmationPopup_NotOptionalEffectPopup()
        {
            // Regression test for the Skeletal Horde double-accept bug: IsPopupVisible
            // (combined) must stay true for either popup - it gates the Main Game UI buttons -
            // but IsConfirmationPopupVisible, which gates UIManager's generic PopupConfirmButtonRect,
            // must be true ONLY for the confirmation popup. The optional-effect popup has its
            // own dedicated Yes/No buttons whose screen bounds overlap PopupConfirmButtonRect;
            // if both were gated on the combined flag, a single click fired both handlers.

            // Only the optional-effect popup is open.
            _mediator.RequestOptionalEffect(null!, null!, () => { }, () => { });
            Assert.IsTrue(_mediator.IsOptionalEffectPopupOpen);
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);

            _mediator.Update();

            _uiManager.Received(1).IsPopupVisible = true;
            _uiManager.Received(1).IsConfirmationPopupVisible = false;

            // Now only the confirmation popup is open.
            _uiManager.ClearReceivedCalls();
            SetPrivateField(_mediator, "_isOptionalEffectPopupOpen", false);
            SetPrivateField(_mediator, "_isConfirmationPopupOpen", true);

            _mediator.Update();

            _uiManager.Received(1).IsPopupVisible = true;
            _uiManager.Received(1).IsConfirmationPopupVisible = true;
        }

        // Helper
        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new ArgumentException($"Field '{fieldName}' not found");
            field.SetValue(target, value);
        }
    }
}
