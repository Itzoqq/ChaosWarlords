using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
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
        private IGameplayState _mockGameState = null!;
        private IUIManager _mockUIManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private UIEventMediator _mediator = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockGameState = Substitute.For<IGameplayState>();
            _mockUIManager = Substitute.For<IUIManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();

            // Additional Mocks for MatchContext
            var mockTurn = Substitute.For<ITurnManager>();
            var mockMap = Substitute.For<IMapManager>();
            var mockMarket = Substitute.For<IMarketManager>();
            var mockDb = Substitute.For<ICardDatabase>();

            // Setup recursively accessible properties
            _mockGameState.TurnManager.Returns(mockTurn);
            _mockGameState.MapManager.Returns(mockMap);
            _mockGameState.MarketManager.Returns(mockMarket);
            _mockGameState.MatchManager.Returns(Substitute.For<IMatchManager>());

            // Construct concrete MatchContext
            var matchContext = new MatchContext(
                mockTurn,
                mockMap,
                mockMarket,
                _mockActionSystem,
                mockDb,
                new PlayerStateManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance),
                null, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _mockGameState.MatchContext.Returns(matchContext);

            // Setup ActivePlayer (returning a real Player object is easiest)
            var player = TestData.Players.RedPlayer();
            mockTurn.ActivePlayer.Returns(player);

            // Mock TurnContext for promotion check
            var turnContext = new TurnContext(player, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            mockTurn.CurrentTurnContext.Returns(turnContext);

            _mediator = new UIEventMediator(_mockGameState, _mockUIManager, _mockActionSystem, ChaosWarlords.Tests.Utilities.TestLogger.Instance, null!);
        }

        [TestMethod]
        public void Initialize_CanBeCalledWithoutError()
        {
            // Act
            _mediator.Initialize();

            // Assert - No exception thrown
            Assert.IsNotNull(_mediator);
        }

        [TestMethod]
        public void Cleanup_CanBeCalledWithoutError()
        {
            // Arrange
            _mediator.Initialize();

            // Act
            _mediator.Cleanup();

            // Assert - No exception thrown
            Assert.IsNotNull(_mediator);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_WhenClosed_OpensMenu()
        {
            // Arrange
            _mockGameState.IsPauseMenuOpen.Returns(false);

            // Act
            _mediator.HandleEscapeKeyPress();

            // Assert - Verify internal state changed
            Assert.IsTrue(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void HandleEscapeKeyPress_WhenOpen_ClosesMenu()
        {
            // Arrange - First open the menu
            _mockGameState.IsPauseMenuOpen.Returns(false);
            _mediator.HandleEscapeKeyPress();

            // Now close it
            _mockGameState.IsPauseMenuOpen.Returns(true);

            // Act
            _mediator.HandleEscapeKeyPress();

            // Assert - Verify internal state changed
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }

        [TestMethod]
        public void Update_CanBeCalledWithoutError()
        {
            // Act
            _mediator.Update();

            // Assert - No exception thrown
            Assert.IsNotNull(_mediator);
        }

        [TestMethod]
        public void IsConfirmationPopupOpen_InitiallyFalse()
        {
            // Assert
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
        }

        [TestMethod]
        public void IsPauseMenuOpen_InitiallyFalse()
        {
            // Assert
            Assert.IsFalse(_mediator.IsPauseMenuOpen);
        }
        [TestMethod]
        public void HandleMarketToggle_DelegatesToGameState()
        {
            // Arrange
            _mediator.Initialize();

            // Act
            _mockUIManager.OnMarketToggleRequest += Raise.Event();

            // Assert
            _mockGameState.Received(1).ToggleMarket();
        }

        [TestMethod]
        public void HandleAssassinateRequest_StartsTargeting()
        {
            // Arrange
            _mediator.Initialize();
            _mockActionSystem.IsTargeting().Returns(true); // Simulate successful start

            // Act
            _mockUIManager.OnAssassinateRequest += Raise.Event();

            // Assert
            _mockActionSystem.Received(1).TryStartAssassinate();
            _mockGameState.Received(1).SwitchToTargetingMode();
        }

        [TestMethod]
        public void HandleReturnSpyRequest_StartsTargeting()
        {
            // Arrange
            _mediator.Initialize();
            _mockActionSystem.IsTargeting().Returns(true); // Simulate successful start

            // Act
            _mockUIManager.OnReturnSpyRequest += Raise.Event();

            // Assert
            _mockActionSystem.Received(1).TryStartReturnSpy();
            _mockGameState.Received(1).SwitchToTargetingMode();
        }

        [TestMethod]
        public void HandleEndTurnRequest_OpensPopup_WhenCardsUnplayed()
        {
            // Arrange
            _mediator.Initialize();
            // Add a card to the real player's hand
            var player = _mockGameState.MatchContext.ActivePlayer;
            player.Hand.Add(TestData.Cards.CheapCard());

            // Act
            _mockUIManager.OnEndTurnRequest += Raise.Event();

            // Assert
            Assert.IsTrue(_mediator.IsConfirmationPopupOpen);
        }

        [TestMethod]
        public void HandleEndTurnRequest_EndsTurn_WhenNoCardsUnplayed()
        {
            // Arrange
            _mediator.Initialize();
            // Ensure hand is empty (it is by default)
            var player = _mockGameState.MatchContext.ActivePlayer;
            player.Hand.Clear();


            // Mock no pending promotions
            // _mockGameState.TurnManager.CurrentTurnContext.PendingPromotionsCount is 0 by default (concrete class)

            // Act
            _mockUIManager.OnEndTurnRequest += Raise.Event();

            // Assert
            _mockGameState.Received(1).RecordAndExecuteCommand(Arg.Any<EndTurnCommand>());
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
        }

        [TestMethod]
        public void HandlePopupConfirm_EndsTurn()
        {
            // Arrange
            _mediator.Initialize();
            // Add card to trigger popup logic
            var player = _mockGameState.MatchContext.ActivePlayer;
            player.Hand.Add(new CardBuilder().WithName("test").WithCost(1).WithAspect(CardAspect.Warlord).Build());
            _mediator.HandleEndTurnKeyPress(); // Open popup

            Assert.IsTrue(_mediator.IsConfirmationPopupOpen, "Popup should be open");

            // Mock no pending promotions for simple EndTurn
            // _mockGameState.TurnManager.CurrentTurnContext.PendingPromotionsCount is 0 by default

            // Act
            _mockUIManager.OnPopupConfirm += Raise.Event();

            // Assert
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            _mockGameState.Received(1).RecordAndExecuteCommand(Arg.Any<EndTurnCommand>());
        }

        [TestMethod]
        public void HandlePopupCancel_ClosesPopup()
        {
            // Arrange
            _mediator.Initialize();
            var player = _mockGameState.MatchContext.ActivePlayer;
            player.Hand.Add(new CardBuilder().WithName("test").WithCost(1).WithAspect(CardAspect.Warlord).Build());
            _mediator.HandleEndTurnKeyPress(); // Open popup

            Assert.IsTrue(_mediator.IsConfirmationPopupOpen);

            // Act
            _mockUIManager.OnPopupCancel += Raise.Event();

            // Assert
            Assert.IsFalse(_mediator.IsConfirmationPopupOpen);
            _mockGameState.DidNotReceive().EndTurn();
        }

        [TestMethod]
        public void HandleActionCompleted_PlaysPendingCard_AndResetsMode()
        {
            // Arrange
            _mediator.Initialize();
            var card = TestData.Cards.CheapCard();
            _mockActionSystem.PendingCard.Returns(card);

            // Act
            _mockActionSystem.OnActionCompleted += Raise.Event();

            // Assert
            _mockGameState.MatchManager.Received(1).PlayCard(card);
            _mockActionSystem.DidNotReceive().CancelTargeting();
            _mockGameState.Received(1).SwitchToNormalMode();
        }
    }
}



