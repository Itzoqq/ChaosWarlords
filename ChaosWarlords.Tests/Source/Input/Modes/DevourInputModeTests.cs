using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Input.Modes
{
    [TestClass]

    [TestCategory("Integration")]
    public class DevourInputModeTests
    {
        private IGameplayState _mockGameState = null!;
        private IInputManager _mockInputManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private IMatchManager _mockMatchManager = null!;
        private DevourInputMode _mode = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockGameState = Substitute.For<IGameplayState>();
            _mockInputManager = Substitute.For<IInputManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();
            _mockMatchManager = Substitute.For<IMatchManager>();

            _mockGameState.MatchManager.Returns(_mockMatchManager);

            _mode = new DevourInputMode(_mockGameState, _mockInputManager, _mockActionSystem);
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
            _mockGameState.Received(1).SwitchToNormalMode();
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
            _mockGameState.Received(1).SwitchToNormalMode();
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
            _mockGameState.GetHoveredHandCard().Returns(targetCard);

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(targetCard);
            // Default mock behavior for IsTargeting is false
            _mockGameState.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void HandleInput_SwitchesToTargeting_WhenActionChains()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            var targetCard = TestData.Cards.CheapCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockGameState.GetHoveredHandCard().Returns(targetCard);
            
            // SIMULATE CHAIN: ActionSystem is now targeting (e.g. Supplant)
            _mockActionSystem.IsTargeting().Returns(true);

            // Act
            _mode.HandleInput(_mockInputManager, null!, null!, activePlayer: null!, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(targetCard);
            // Standard Flow: InputMode detects chaining and switches directly
            _mockGameState.Received(1).SwitchToTargetingMode();
            _mockGameState.DidNotReceive().SwitchToNormalMode();
        }

        [TestMethod]
        public void HandleInput_LogsWarning_WhenDevouringSelf()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockGameState.GetHoveredHandCard().Returns(sourceCard); // Same card

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert - The implementation logs a warning but doesn't prevent the action
            // It still calls DevourCard and CompleteAction
            // Assert - The implementation logs a warning. 
            // In the new implementation, we call HandleDevourSelection, which does the checks.
            _mockActionSystem.Received(1).HandleDevourSelection(sourceCard);
        }

        [TestMethod]
        public void HandleInput_DoesNothing_WhenNoCardHovered()
        {
            // Arrange
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockGameState.GetHoveredHandCard().Returns((Card)null!);

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
            var mode = new DevourInputMode(_mockGameState, _mockInputManager, _mockActionSystem);

            // Act
            var result = mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            // 1. Check SkippedTarget was set
            _mockActionSystem.Received(1).SetPreTarget(sourceCard, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);
            
            // 2. Check Action Completed (Exit Targeting)
            _mockActionSystem.Received(1).CompleteAction();

            // 3. Check Mode Switch
            _mockGameState.Received(1).SwitchToNormalMode();

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
            _mockGameState.GetHoveredHandCard().Returns(targetCard);

            // Re-create mode
            var mode = new DevourInputMode(_mockGameState, _mockInputManager, _mockActionSystem);

            // Act
            var result = mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            // 1. Check Target was set
            _mockActionSystem.Received(1).SetPreTarget(sourceCard, ActionState.TargetingDevourHand, targetCard);

            // 2. Check Action Completed
            _mockActionSystem.Received(1).CompleteAction();

            // 3. Check Mode Switch
            _mockGameState.Received(1).SwitchToNormalMode();

            // 4. Check Play Command returned
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ChaosWarlords.Source.Commands.PlayCardCommand));
            var cmd = (ChaosWarlords.Source.Commands.PlayCardCommand)result;
            Assert.IsTrue(cmd.BypassChecks, "Command should have BypassChecks set to true for Target Selection.");
        }
    }
}



