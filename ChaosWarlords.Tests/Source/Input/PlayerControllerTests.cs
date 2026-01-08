using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Input;

namespace ChaosWarlords.Tests.Source.Input
{
    [TestClass]
    public class PlayerControllerTests
    {
        private PlayerController _controller = null!;
        private IGameplayState _gameState = null!;
        private IInputManager _inputManager = null!;
        private IGameplayInputCoordinator _inputCoordinator = null!;
        private IInteractionMapper _interactionMapper = null!;

        [TestInitialize]
        public void Setup()
        {
            _gameState = Substitute.For<IGameplayState>();
            _inputManager = Substitute.For<IInputManager>();
            _inputCoordinator = Substitute.For<IGameplayInputCoordinator>();
            _interactionMapper = Substitute.For<IInteractionMapper>();

            _controller = new PlayerController(
                _gameState,
                _inputManager,
                _inputCoordinator,
                _interactionMapper
            );
        }

        [TestMethod]
        public void Update_ProceedsToCoordinator_WhenMarketIsOpen()
        {
            // Arrange
            _gameState.IsMarketOpen.Returns(true);

            // Act
            bool result = _controller.Update();

            // Assert
            Assert.IsFalse(result, "Update should return false (not blocked) when Market is open, to allow MarketInputMode to run.");
            _inputCoordinator.Received(1).HandleInput(); // Ensure input is delegated
        }

        [TestMethod]
        public void Update_ReturnsTrue_WhenOptionalEffectPopupIsOpen()
        {
            // Arrange
            _gameState.IsOptionalEffectPopupOpen.Returns(true);

            // Act
            bool result = _controller.Update();

            // Assert
            Assert.IsTrue(result, "Update should return true (blocked) when Optional Effect Popup is open.");
            _inputCoordinator.DidNotReceive().HandleInput();
        }

        [TestMethod]
        public void Update_ReturnsTrue_WhenPauseMenuIsOpen()
        {
            // Arrange
            _gameState.IsPauseMenuOpen.Returns(true);

            // Act
            bool result = _controller.Update();

            // Assert
            Assert.IsTrue(result, "Update should return true (blocked) when Pause Menu is open.");
            _inputCoordinator.DidNotReceive().HandleInput();
        }

        [TestMethod]
        public void Update_ReturnsTrue_WhenConfirmationPopupIsOpen()
        {
            // Arrange
            _gameState.IsConfirmationPopupOpen.Returns(true);

            // Act
            bool result = _controller.Update();

            // Assert
            Assert.IsTrue(result, "Update should return true (blocked) when Confirmation Popup is open.");
            _inputCoordinator.DidNotReceive().HandleInput();
        }

        [TestMethod]
        public void HandleEnterKey_BlocksEndTurn_WhenMarketIsOpen()
        {
            // Arrange
            _gameState.IsMarketOpen.Returns(true);
            _inputManager.IsKeyJustPressed(Keys.Enter).Returns(true);

            // Act
            bool result = _controller.Update(); // Update calls HandleGlobalInput -> HandleEnterKey

            // Assert
            Assert.IsTrue(result, "Input should be handled/blocked.");
            _gameState.DidNotReceive().HandleEndTurnKeyPress(); // Should NOT call end turn
        }

        [TestMethod]
        public void HandleEnterKey_BlocksEndTurn_WhenOptionalEffectPopupIsOpen()
        {
            // Arrange
            _gameState.IsOptionalEffectPopupOpen.Returns(true);
            _inputManager.IsKeyJustPressed(Keys.Enter).Returns(true);

            // Act
            bool result = _controller.Update();

            // Assert
            Assert.IsTrue(result, "Input should be handled/blocked.");
            _gameState.DidNotReceive().HandleEndTurnKeyPress(); // Should NOT call end turn
        }

        [TestMethod]
        public void HandleEnterKey_CallsEndTurn_WhenOverlaysAreClosed()
        {
            // Arrange
            _gameState.IsMarketOpen.Returns(false);
            _gameState.IsOptionalEffectPopupOpen.Returns(false);
            _gameState.IsPauseMenuOpen.Returns(false);
            _gameState.IsConfirmationPopupOpen.Returns(false);
            
            _inputManager.IsKeyJustPressed(Keys.Enter).Returns(true);
            _gameState.CanEndTurn(out Arg.Any<string>()).Returns(true);

            // Act
            bool result = _controller.Update();

            // Assert
            Assert.IsTrue(result, "Input should be handled.");
            _gameState.Received(1).HandleEndTurnKeyPress(); // SHOULD call end turn
        }
    }
}
