using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework.Input;
using NSubstitute;

namespace ChaosWarlords.Tests.Input.Modes
{
    [TestClass]
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
            var targetCard = new Card("test", "Test Card", 3, CardAspect.Warlord, 1, 1, 0);
            var sourceCard = new Card("source", "Source Card", 5, CardAspect.Shadow, 2, 0, 0);

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockGameState.GetHoveredHandCard().Returns(targetCard);

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert
            _mockMatchManager.Received(1).DevourCard(targetCard);
            _mockActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void HandleInput_LogsWarning_WhenDevouringSelf()
        {
            // Arrange
            var sourceCard = new Card("source", "Source Card", 5, CardAspect.Shadow, 2, 0, 0);

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockGameState.GetHoveredHandCard().Returns(sourceCard); // Same card

            // Act
            var result = _mode.HandleInput(_mockInputManager, null!, null!, null!, _mockActionSystem);

            // Assert - The implementation logs a warning but doesn't prevent the action
            // It still calls DevourCard and CompleteAction
            _mockMatchManager.Received(1).DevourCard(sourceCard);
            _mockActionSystem.Received(1).CompleteAction();
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
    }
}



