using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Tests.Integration.Input.Controllers
{
    [TestClass]
    [TestCategory("Integration")]
    public class PlayerControllerTests
    {
        private TestGameplayState _stateFake = null!;
        private IInputManager _mockInputManager = null!;
        private IGameplayInputCoordinator _mockCoordinator = null!;
        private IInteractionMapper _mockMapper = null!;
        private PlayerController _controller = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInputManager = Substitute.For<IInputManager>();
            _mockCoordinator = Substitute.For<IGameplayInputCoordinator>();
            _mockMapper = Substitute.For<IInteractionMapper>();
            
            // Setup Fake State
            _stateFake = new TestGameplayState
            {
                InputManager = _mockInputManager,
                ActionSystem = Substitute.For<IActionSystem>() 
            };

            _controller = new PlayerController(
                _stateFake,
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
            Assert.IsTrue(_stateFake.EscapeHandled, "State should acknowledge Escape key press.");
            Assert.IsTrue(result, "Should return true when escape is handled");
        }

        [TestMethod]
        public void HandleEnterKey_EndsTurn_WhenAllowed()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Keys.Enter).Returns(true);
            _stateFake.IsPauseMenuOpen = false;
            // TestGameplayState.CanEndTurn defaults to true

            // Act
            var result = _controller.Update();

            // Assert
            Assert.IsTrue(_stateFake.EndTurnRequested, "State should have received EndTurn request.");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleEnterKey_DoesNotEndTurn_WhenPauseMenuOpen()
        {
            // Arrange
            _mockInputManager.IsKeyJustPressed(Keys.Enter).Returns(true);
            _stateFake.IsPauseMenuOpen = true;

            // Act
            var result = _controller.Update();

            // Assert
            Assert.IsFalse(_stateFake.EndTurnRequested, "Should NOT request EndTurn when Pause Menu is not handled by PlayerController here.");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleRightClick_ClosesMarket_WhenOpen()
        {
            // Arrange
            _mockInputManager.IsRightMouseJustClicked().Returns(true);
            _stateFake.IsMarketOpen = true;

            // Act
            var result = _controller.Update();

            // Assert
            Assert.IsFalse(_stateFake.IsMarketOpen, "Market should be closed by right click.");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleRightClick_CancelsTargeting_WhenTargeting()
        {
            // Arrange
            _mockInputManager.IsRightMouseJustClicked().Returns(true);
            _stateFake.IsMarketOpen = false;
            
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.IsTargeting().Returns(true);
            _stateFake.ActionSystem = mockActionSystem; // Inject mock into fake

            // Act
            var result = _controller.Update();

            // Assert
            mockActionSystem.Received(1).CancelTargeting();
            
            // Verify state logic (PlayerController calls SwitchToNormalMode on state)
            Assert.AreEqual("Normal", _stateFake.ActiveModeName, "Should switch to Normal mode.");
            
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HandleSpySelectionInput_FinalizesSpyReturn()
        {
            // Arrange
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = TestData.Sites.CitySite();
            mockActionSystem.PendingSite.Returns(mockSite);
            
            _stateFake.ActionSystem = mockActionSystem;

            _mockInputManager.IsLeftMouseJustClicked().Returns(true);
            _mockInputManager.MousePosition.Returns(new Vector2(100, 100));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

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
