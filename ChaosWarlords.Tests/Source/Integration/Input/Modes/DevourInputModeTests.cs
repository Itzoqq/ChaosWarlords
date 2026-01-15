using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class DevourInputModeTests
    {
        private TestGameplayState _stateFake = null!;
        private IInputManager _mockInputManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private IMatchManager _mockMatchManager = null!;
        private DevourInputMode _mode = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInputManager = Substitute.For<IInputManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();
            _mockMatchManager = Substitute.For<IMatchManager>();

            _stateFake = new TestGameplayState
            {
                MatchManager = _mockMatchManager,
                ActionSystem = _mockActionSystem,
                // MatchContext with dummies if needed, though DevourInputMode might not use it directly
                // based on original test, it only used ActionSystem and State methods.
                // We'll provide a dummy context just in case state internals need it.
                MatchContext = new MatchContext(
                    Substitute.For<ITurnManager>(),
                    Substitute.For<IMapManager>(),
                    Substitute.For<IMarketManager>(),
                    _mockActionSystem,
                    Substitute.For<ICardDatabase>(),
                    new PlayerStateManager(Utilities.TestLogger.Instance),
                    null, Utilities.TestLogger.Instance)
            };

            _mode = new DevourInputMode(_stateFake, _mockInputManager, _mockActionSystem);

            // Warmup because of cooldown
            for (int i = 0; i < 15; i++)
            {
                _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);
            }
        }

        [TestMethod]
        public void HandleInput_CancelsOnRightClick()
        {
            // Arrange
            _mockInputManager.IsRightMouseJustClicked().Returns(true);

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).CancelTargeting();

            // State-based assertion
            Assert.AreEqual("Normal", _stateFake.ActiveModeName, "Should switch to Normal mode on cancel.");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleInput_CancelsOnEscapeKey()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Keys.Escape).Returns(true);

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).CancelTargeting();
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleInput_DevoursCard_WhenValidCardClicked()
        {
            // Arrange
            var targetCard = TestData.Cards.CheapCard();
            var sourceCard = TestData.Cards.DevourCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _stateFake.HoveredHandCard = targetCard;

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(targetCard);
            // Default mock behavior for IsTargeting is false, so it goes to Normal
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);
        }

        [TestMethod]
        public void HandleInput_SwitchesToTargeting_WhenActionChains()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            var targetCard = TestData.Cards.CheapCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _stateFake.HoveredHandCard = targetCard;

            // SIMULATE CHAIN: ActionSystem is now targeting (e.g. Supplant)
            _mockActionSystem.IsTargeting().Returns(true);

            // Act
            _mode.HandleInput(_mockInputManager, null!, null!, activePlayer: null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(targetCard);
            // Standard Flow: InputMode detects chaining and switches directly
            Assert.AreEqual("Targeting", _stateFake.ActiveModeName);
        }

        [TestMethod]
        public void HandleInput_LogsWarning_WhenDevouringSelf()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _stateFake.HoveredHandCard = sourceCard; // Same card

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(sourceCard);
        }

        [TestMethod]
        public void HandleInput_DoesNothing_WhenNoCardHovered()
        {
            // Arrange
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _stateFake.HoveredHandCard = null;

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockMatchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
            _mockActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void HandleInput_Spacebar_SkippedTarget_AndCommits()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            // IMPORTANT: Set location to Hand for Pre-Commit flow
            sourceCard.Location = CardLocation.Hand;

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsKeyJustPressed(Keys.Space).Returns(true);

            // Re-create mode to capture PendingCard
            var mode = new DevourInputMode(_stateFake, _mockInputManager, _mockActionSystem);
            for (int i = 0; i < 15; i++) mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            _mockActionSystem.ClearReceivedCalls();

            // Act
            var result = mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            // 1. Check SkippedTarget was set
            _mockActionSystem.Received(1).SetPreTarget(sourceCard, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            // 2. Check Action Completed (Exit Targeting)
            _mockActionSystem.Received(1).CompleteAction();

            // 3. Check Mode Switch
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);

            // 4. Check Play Command returned
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ChaosWarlords.Source.Commands.PlayCardCommand));
            var cmd = (ChaosWarlords.Source.Commands.PlayCardCommand)result;
            Assert.IsTrue(cmd.BypassChecks, "Command should have BypassChecks set to true for Spacebar skip.");
        }

        [TestMethod]
        public void HandleInput_SelectTarget_PreCommits_AndCommits()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            sourceCard.Location = CardLocation.Hand;
            var targetCard = TestData.Cards.CheapCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _stateFake.HoveredHandCard = targetCard;

            // Re-create mode
            var mode = new DevourInputMode(_stateFake, _mockInputManager, _mockActionSystem);
            for (int i = 0; i < 15; i++) mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            _mockActionSystem.ClearReceivedCalls();

            // Act
            var result = mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            // 1. Check Target was set
            _mockActionSystem.Received(1).SetPreTarget(sourceCard, ActionState.TargetingDevourHand, targetCard);

            // 2. Check Action Completed
            _mockActionSystem.Received(1).CompleteAction();

            // 3. Check Mode Switch
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);

            // 4. Check Play Command returned
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ChaosWarlords.Source.Commands.PlayCardCommand));
            var cmd = (ChaosWarlords.Source.Commands.PlayCardCommand)result;
            Assert.IsTrue(cmd.BypassChecks, "Command should have BypassChecks set to true for Target Selection.");
        }
    }
}
