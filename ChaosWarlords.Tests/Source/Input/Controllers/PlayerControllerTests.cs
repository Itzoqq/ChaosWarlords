using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.States;

using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Input.Controllers
{
    [TestClass]
    public class PlayerControllerTests
    {
        private IGameplayState _mockGameState = null!;
        private IInputManager _mockInputManager = null!;
        private IGameplayInputCoordinator _mockCoordinator = null!;
        private IInteractionMapper _mockMapper = null!;
        private PlayerController _controller = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockGameState = Substitute.For<IGameplayState>();
            _mockInputManager = Substitute.For<IInputManager>();
            _mockCoordinator = Substitute.For<IGameplayInputCoordinator>();
            _mockMapper = Substitute.For<IInteractionMapper>();

            _controller = new PlayerController(
                _mockGameState,
                _mockInputManager,
                _mockCoordinator,
                _mockMapper);
        }

        [TestMethod]
        public void Update_DelegatesToInputCoordinator()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Arg.Any<Keys>()).Returns(false);
            _mockInputManager.IsRightMouseJustClicked().Returns(false);

            // Act
            _controller.Update();

            // Assert
            _mockCoordinator.Received(1).HandleInput();
        }

        [TestMethod]
        public void HandleEscapeKey_CallsGameStateEscapeHandler()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Keys.Escape).Returns(true);

            // Act
            var result = _controller.Update();

            // Assert
            _mockGameState.Received(1).HandleEscapeKeyPress();
            Assert.IsTrue(result, "Should return true when escape is handled");
        }

        [TestMethod]
        public void HandleEnterKey_EndsTurn_WhenAllowed()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Keys.Enter).Returns(true);
            _mockGameState.IsPauseMenuOpen.Returns(false);
            _mockGameState.CanEndTurn(out Arg.Any<string>()).Returns(x =>
            {
                x[0] = "";
                return true;
            });

            // Act
            var result = _controller.Update();

            // Assert
            _mockGameState.Received(1).HandleEndTurnKeyPress();
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleEnterKey_DoesNotEndTurn_WhenPauseMenuOpen()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Keys.Enter).Returns(true);
            _mockGameState.IsPauseMenuOpen.Returns(true);

            // Act
            var result = _controller.Update();

            // Assert
            _mockGameState.DidNotReceive().HandleEndTurnKeyPress();
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleRightClick_ClosesMarket_WhenOpen()
        {
            // Arrange
            _mockInputManager.IsRightMouseJustClicked().Returns(true);
            _mockGameState.IsMarketOpen.Returns(true);

            // Act
            var result = _controller.Update();

            // Assert
            _mockGameState.Received(1).CloseMarket();
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleRightClick_CancelsTargeting_WhenTargeting()
        {
            // Arrange
            _mockInputManager.IsRightMouseJustClicked().Returns(true);
            _mockGameState.IsMarketOpen.Returns(false);
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.IsTargeting().Returns(true);
            _mockGameState.ActionSystem.Returns(mockActionSystem);

            // Act
            var result = _controller.Update();

            // Assert
            mockActionSystem.Received(1).CancelTargeting();
            _mockGameState.Received(1).SwitchToNormalMode();
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleSpySelectionInput_FinalizesSpyReturn()
        {
            // Arrange
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = Substitute.For<ChaosWarlords.Source.Entities.Map.Site>("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 1);
            mockActionSystem.PendingSite.Returns(mockSite);
            _mockGameState.ActionSystem.Returns(mockActionSystem);

            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockInputManager.MousePosition.Returns(new Vector2(100, 100));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _mockGameState.UIManager.Returns(mockUIManager);

            _mockMapper.GetClickedSpyReturnButton(Arg.Any<Point>(), mockSite, 800)
                .Returns(PlayerColor.Blue);

            // Act
            var result = _controller.Update();

            // Assert
            mockActionSystem.Received(1).FinalizeSpyReturn(PlayerColor.Blue);
            Assert.IsTrue(result);
        }
    }
}


