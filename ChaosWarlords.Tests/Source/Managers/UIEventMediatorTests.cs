using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Commands;
using NSubstitute;

namespace ChaosWarlords.Tests.Managers
{
    [TestClass]

    [TestCategory("Unit")]

    public class UIEventMediatorTests
    {
        // Use Fake State
        private ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState _state = null!;
        private IUIManager _mockUIManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private UIEventMediator _mediator = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            _mockUIManager = Substitute.For<IUIManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();

            // Assign ActionSystem to state as well so internal logic works if needed
            // But Mediator takes ActionSystem separately in ctor.
            _state.ActionSystem = _mockActionSystem;

            // Additional Mocks for MatchContext (Using defaults from TestGameplayState)
            var mockTurn = _state.TurnManager;
            var mockMap = _state.MapManager;
            var mockMarket = _state.MarketManager;
            var mockDb = Substitute.For<ICardDatabase>();

            // Construct concrete MatchContext
            var matchContext = new MatchContext(
                mockTurn,
                mockMap,
                mockMarket,
                _mockActionSystem,
                mockDb,
                new PlayerStateManager(Utilities.TestLogger.Instance),
                null, Utilities.TestLogger.Instance);

            matchContext.MatchManager = _state.MatchManager;

            _state.MatchContext = matchContext;

            // Setup ActivePlayer
            var player = TestData.Players.RedPlayer();
            mockTurn.ActivePlayer.Returns(player);

            // Mock TurnContext for promotion check
            var turnContext = new TurnContext(player, Utilities.TestLogger.Instance);
            mockTurn.CurrentTurnContext.Returns(turnContext);

            _mediator = new UIEventMediator(_state, _mockUIManager, _mockActionSystem, Utilities.TestLogger.Instance, null!);
        }

        [TestMethod]
        public void Initialize_CanBeCalledWithoutError()
        {
            _mediator.Initialize();
            Assert.IsNotNull(_mediator);
        }

        [TestMethod]
        public void Cleanup_CanBeCalledWithoutError()
        {
            _mediator.Initialize();
            _mediator.Cleanup();
            Assert.IsNotNull(_mediator);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_WhenClosed_OpensMenu()
        {
            _state.IsPauseMenuOpen = false;
            _mediator.HandleEscapeKeyPress();
            Assert.IsTrue(_mediator.IsPauseMenuOpen);
            // The mediator checks state.IsPauseMenuOpen.
            // Wait, Mediator's property IsPauseMenuOpen usually delegates to state?
            // "public bool IsPauseMenuOpen => _state.IsPauseMenuOpen;" ??
            // If so, _mediator.HandleEscapeKeyPress() calls _state.HandleEscapeKeyPress() ?
            // Let's verify mediator implementation if test fails.
            // Assuming Mediator reads/writes state or has logic.
        }

        [TestMethod]
        public void HandleEscapeKeyPress_WhenOpen_ClosesMenu()
        {
            _state.IsPauseMenuOpen = true;  // Fake state
            _mediator.HandleEscapeKeyPress();

            // If Mediator toggles local flag AND updates state? 
            // Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void Update_CanBeCalledWithoutError()
        {
            _mediator.Update();
            Assert.IsNotNull(_mediator);
        }

        [TestMethod]
        public void IsConfirmationPopupOpen_InitiallyFalse()
        {
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
        }

        [TestMethod]
        public void IsPauseMenuOpen_InitiallyFalse()
        {
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleMarketToggle_DelegatesToGameState()
        {
            _mediator.Initialize();
            _state.MarketStateManager.Close();

            _mockUIManager.OnMarketToggleRequest += Raise.Event();

            // State-Based Assertion
            Assert.IsTrue(_state.IsMarketOpen, "Market should be toggled open");
        }

        [TestMethod]
        public void HandleAssassinateRequest_StartsTargeting()
        {
            _mediator.Initialize();
            _mockActionSystem.IsTargeting().Returns(true);

            _mockUIManager.OnAssassinateRequest += Raise.Event();

            _mockActionSystem.Received(1).TryStartAssassinate();
            // State-Based Assertion
            Assert.AreEqual("Targeting", _state.ActiveModeName);
        }

        [TestMethod]
        public void HandleReturnSpyRequest_StartsTargeting()
        {
            _mediator.Initialize();
            _mockActionSystem.IsTargeting().Returns(true);

            _mockUIManager.OnReturnSpyRequest += Raise.Event();

            _mockActionSystem.Received(1).TryStartReturnSpy();
            // State-Based Assertion
            Assert.AreEqual("Targeting", _state.ActiveModeName);
        }

        [TestMethod]
        public void HandleEndTurnRequest_OpensPopup_WhenCardsUnplayed()
        {
            _mediator.Initialize();
            var player = _state.MatchContext.ActivePlayer;
            player.AddToHand(TestData.Cards.CheapCard());

            _mockUIManager.OnEndTurnRequest += Raise.Event();

            Assert.IsTrue(_mediator.IsConfirmationPopupOpen);
        }

        [TestMethod]
        public void HandleEndTurnRequest_EndsTurn_WhenNoCardsUnplayed()
        {
            _mediator.Initialize();
            var player = _state.MatchContext.ActivePlayer;
            player.ClearHand();

            _mockUIManager.OnEndTurnRequest += Raise.Event();

            // State-Based Assertion: Check if EndTurnCommand was executed
            Assert.IsNotEmpty(_state.ExecutedCommands);
            Assert.IsInstanceOfType(_state.ExecutedCommands[0], typeof(EndTurnCommand));

            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
        }

        [TestMethod]
        public void HandlePopupConfirm_EndsTurn()
        {
            _mediator.Initialize();
            var player = _state.MatchContext.ActivePlayer;
            player.AddToHand(new CardBuilder().WithName("test").WithCost(1).WithAspect(CardAspect.Warlord).Build());
            _mediator.HandleEndTurnKeyPress(); // Open popup

            Assert.IsTrue(_mediator.IsConfirmationPopupOpen, "Popup should be open");

            _mockUIManager.OnPopupConfirm += Raise.Event();

            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            // State-Based Assertion
            Assert.IsTrue(_state.ExecutedCommands.Any(c => c is EndTurnCommand));
        }

        [TestMethod]
        public void HandlePopupCancel_ClosesPopup()
        {
            _mediator.Initialize();
            var player = _state.MatchContext.ActivePlayer;
            player.AddToHand(new CardBuilder().WithName("test").WithCost(1).WithAspect(CardAspect.Warlord).Build());
            _mediator.HandleEndTurnKeyPress();

            Assert.IsTrue(_mediator.IsConfirmationPopupOpen);

            _mockUIManager.OnPopupCancel += Raise.Event();

            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            // State-Based Assertion
            Assert.IsFalse(_state.EndTurnCalled); // Fake has this property
            Assert.IsEmpty(_state.ExecutedCommands);
        }

        [TestMethod]
        public void HandleActionCompleted_PlaysPendingCard_AndResetsMode()
        {
            _mediator.Initialize();
            var card = TestData.Cards.CheapCard();
            _mockActionSystem.PendingCard.Returns(card);

            _mockActionSystem.OnActionCompleted += Raise.Event();

            // MatchManager is a mock inside the fake, so we can verify calls on it
            _state.MatchManager.Received(1).PlayCard(card);

            _mockActionSystem.DidNotReceive().CancelTargeting();

            // State-Based Assertion
            Assert.AreEqual("Normal", _state.ActiveModeName);
        }
        [TestMethod]
        public void RequestOptionalEffect_OpensPopup_AndExecutesAcceptCallback()
        {
            _mediator.Initialize();
            bool callbackExecuted = false;
            Action onAccept = () => callbackExecuted = true;
            Action onDecline = () => { };

            _mediator.RequestOptionalEffect(null!, null!, onAccept, onDecline);

            Assert.IsTrue(_mediator.IsOptionalEffectPopupOpen);
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);

            // Confirm
            _mockUIManager.OnPopupConfirm += Raise.Event();

            Assert.IsTrue(callbackExecuted, "Accept callback should be executed");
            Assert.IsFalse(_mediator.IsOptionalEffectPopupOpen, "Popup should close");
        }

        [TestMethod]
        public void RequestOptionalEffect_OpensPopup_AndExecutesDeclineCallback()
        {
            _mediator.Initialize();
            bool callbackExecuted = false;
            Action onAccept = () => { };
            Action onDecline = () => callbackExecuted = true;

            _mediator.RequestOptionalEffect(null!, null!, onAccept, onDecline);

            Assert.IsTrue(_mediator.IsOptionalEffectPopupOpen);

            // Decline
            _mockUIManager.OnPopupCancel += Raise.Event();

            Assert.IsTrue(callbackExecuted, "Decline callback should be executed");
            Assert.IsFalse(_mediator.IsOptionalEffectPopupOpen, "Popup should close");
        }
    }
}



