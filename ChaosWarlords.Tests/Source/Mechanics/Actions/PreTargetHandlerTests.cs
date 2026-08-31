using NSubstitute;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Mechanics.Actions
{
    [TestClass]
    [TestCategory("Unit")]
    public class PreTargetHandlerTests
    {
        private IGameLogger _mockLogger = null!;
        private Dictionary<Card, Dictionary<ActionState, object>> _preTargets = null!;
        private PreTargetHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = Substitute.For<IGameLogger>();
            _preTargets = new Dictionary<Card, Dictionary<ActionState, object>>();
            _handler = new PreTargetHandler(_mockLogger, _preTargets);
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            try
            {
                new PreTargetHandler(null!, _preTargets);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Constructor_WithNullPreTargets_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            try
            {
                new PreTargetHandler(_mockLogger, null!);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            // Arrange & Act
            var handler = new PreTargetHandler(_mockLogger, _preTargets);

            // Assert
            Assert.IsNotNull(handler);
        }

        #endregion

        #region TryExecutePreTarget - No Target Tests

        [TestMethod]
        public void TryExecutePreTarget_WithNoPreTargetForCard_ReturnsFalse()
        {
            // Arrange
            var card = TestData.Cards.PowerCard();
            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                card,
                ActionState.TargetingSupplant,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsFalse(result);
            mockHandleClick.DidNotReceive().Invoke(Arg.Any<MapNode?>(), Arg.Any<Site?>());
            mockHandleDevour.DidNotReceive().Invoke(Arg.Any<Card?>());
        }

        [TestMethod]
        public void TryExecutePreTarget_WithNoPreTargetForState_ReturnsFalse()
        {
            // Arrange
            var card = TestData.Cards.PowerCard();
            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingSupplant] = TestData.MapNodes.Node1()
            };

            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                card,
                ActionState.TargetingAssassinate, // Different state
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region TryExecutePreTarget - MapNode Target Tests

        [TestMethod]
        public void TryExecutePreTarget_WithMapNodeTarget_ExecutesHandleTargetClick()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            var targetNode = TestData.MapNodes.Node1();
            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingSupplant] = targetNode
            };

            var mockCommand = Substitute.For<IGameCommand>();
            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            mockHandleClick.Invoke(targetNode, null).Returns(mockCommand);

            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                card,
                ActionState.TargetingSupplant,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsTrue(result);
            mockHandleClick.Received(1).Invoke(targetNode, null);
            mockOnExecute.Received(1).Invoke(mockCommand);
            _mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("Pre-Target found")), LogChannel.Info);
        }

        [TestMethod]
        public void TryExecutePreTarget_WithMapNodeTarget_ConsumesPreTarget()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            var targetNode = TestData.MapNodes.Node1();
            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingSupplant] = targetNode
            };

            var mockCommand = Substitute.For<IGameCommand>();
            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            mockHandleClick.Invoke(Arg.Any<MapNode?>(), Arg.Any<Site?>()).Returns(mockCommand);

            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            _handler.TryExecutePreTarget(card, ActionState.TargetingSupplant, mockHandleClick, mockHandleDevour, mockOnExecute, mockOnSkipped);

            // Assert - Pre-target should be consumed
            Assert.IsFalse(_preTargets.ContainsKey(card));
        }

        #endregion

        #region TryExecutePreTarget - Site Target Tests

        [TestMethod]
        public void TryExecutePreTarget_WithSiteTarget_ExecutesHandleTargetClick()
        {
            // Arrange
            var card = TestData.Cards.AssassinCard();
            var targetSite = TestData.Sites.PowerCity();
            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingAssassinate] = targetSite
            };

            var mockCommand = Substitute.For<IGameCommand>();
            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            mockHandleClick.Invoke(null, targetSite).Returns(mockCommand);

            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                card,
                ActionState.TargetingAssassinate,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsTrue(result);
            mockHandleClick.Received(1).Invoke(null, targetSite);
            mockOnExecute.Received(1).Invoke(mockCommand);
        }

        #endregion

        #region TryExecutePreTarget - Devour Target Tests

        [TestMethod]
        public void TryExecutePreTarget_WithDevourCardTarget_ExecutesHandleDevourSelection()
        {
            // Arrange
            var sourceCard = new CardBuilder().WithName("wight").Build();
            var targetCard = TestData.Cards.CheapCard();
            _preTargets[sourceCard] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingDevourHand] = targetCard
            };

            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            var mockCommand = Substitute.For<IGameCommand>();
            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            mockHandleDevour.Invoke(targetCard).Returns(mockCommand);
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                sourceCard,
                ActionState.TargetingDevourHand,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsTrue(result);
            mockHandleDevour.Received(1).Invoke(targetCard);
            mockOnExecute.Received(1).Invoke(mockCommand);
            mockHandleClick.DidNotReceive().Invoke(Arg.Any<MapNode?>(), Arg.Any<Site?>());
        }

        [TestMethod]
        public void TryExecutePreTarget_WithDevourSkippedTarget_InvokesOnSkipped()
        {
            // Regression test: skip used to call handleDevourSelection(null), which always
            // returns null (DevourSubsystem.HandleDevourSelection's null guard) - meaning
            // nothing ever resolved the pending EffectContext, leaving ActionSystem's
            // ExecutionStack/CurrentState stuck on TargetingDevourHand forever (the
            // pre-target was already consumed, so nothing would ever retry it). Skip must
            // resolve via onSkipped instead - never call handleDevourSelection at all.
            var sourceCard = new CardBuilder().WithName("wight").Build();
            _preTargets[sourceCard] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingDevourHand] = ActionSystem.SkippedTarget
            };

            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                sourceCard,
                ActionState.TargetingDevourHand,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsTrue(result);
            mockOnSkipped.Received(1).Invoke();
            mockHandleDevour.DidNotReceive().Invoke(Arg.Any<Card?>());
            mockOnExecute.DidNotReceive().Invoke(Arg.Any<IGameCommand>());
        }

        [TestMethod]
        public void TryExecutePreTarget_WithInvalidDevourTarget_LogsWarning()
        {
            // Arrange
            var sourceCard = new CardBuilder().WithName("wight").Build();
            _preTargets[sourceCard] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingDevourHand] = "invalid_target" // Wrong type
            };

            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                sourceCard,
                ActionState.TargetingDevourHand,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("Invalid devour target")), LogChannel.Warning);
        }

        #endregion

        #region TryExecutePreTarget - Unknown Target Type Tests

        [TestMethod]
        public void TryExecutePreTarget_WithUnknownTargetType_LogsWarning()
        {
            // Arrange
            var card = TestData.Cards.PowerCard();
            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingSupplant] = "unknown_type" // Not MapNode, Site, or Card
            };

            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            var result = _handler.TryExecutePreTarget(
                card,
                ActionState.TargetingSupplant,
                mockHandleClick,
                mockHandleDevour,
                mockOnExecute,
                mockOnSkipped);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.Received().Log(Arg.Is<string>(s => s.Contains("Unknown target type")), LogChannel.Warning);
        }

        #endregion

        #region Target Consumption Tests

        [TestMethod]
        public void TryExecutePreTarget_WithMultipleStates_OnlyConsumesSpecifiedState()
        {
            // Arrange
            var card = new CardBuilder().WithName("multi_effect").Build();
            var node1 = TestData.MapNodes.Node1();
            var node2 = TestData.MapNodes.Node2();

            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingSupplant] = node1,
                [ActionState.TargetingMoveSource] = node2
            };

            var mockCommand = Substitute.For<IGameCommand>();
            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            mockHandleClick.Invoke(Arg.Any<MapNode?>(), Arg.Any<Site?>()).Returns(mockCommand);

            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            _handler.TryExecutePreTarget(card, ActionState.TargetingSupplant, mockHandleClick, mockHandleDevour, mockOnExecute, mockOnSkipped);

            // Assert
            Assert.IsTrue(_preTargets.ContainsKey(card));
            Assert.IsFalse(_preTargets[card].ContainsKey(ActionState.TargetingSupplant));
            Assert.IsTrue(_preTargets[card].ContainsKey(ActionState.TargetingMoveSource));
        }

        [TestMethod]
        public void TryExecutePreTarget_WithLastState_RemovesCardEntry()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            var targetNode = TestData.MapNodes.Node1();
            _preTargets[card] = new Dictionary<ActionState, object>
            {
                [ActionState.TargetingSupplant] = targetNode
            };

            var mockCommand = Substitute.For<IGameCommand>();
            var mockHandleClick = Substitute.For<Func<MapNode?, Site?, IGameCommand?>>();
            mockHandleClick.Invoke(Arg.Any<MapNode?>(), Arg.Any<Site?>()).Returns(mockCommand);

            var mockHandleDevour = Substitute.For<Func<Card?, IGameCommand?>>();
            var mockOnExecute = Substitute.For<Action<IGameCommand>>();
            var mockOnSkipped = Substitute.For<Action>();

            // Act
            _handler.TryExecutePreTarget(card, ActionState.TargetingSupplant, mockHandleClick, mockHandleDevour, mockOnExecute, mockOnSkipped);

            // Assert - Card should be completely removed
            Assert.IsFalse(_preTargets.ContainsKey(card));
        }

        #endregion
    }
}
