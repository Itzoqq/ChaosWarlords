using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Interfaces;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Managers
{
    [TestClass]
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

            _mediator = new UIEventMediator(_mockGameState, _mockUIManager, _mockActionSystem, null);
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
    }
}
