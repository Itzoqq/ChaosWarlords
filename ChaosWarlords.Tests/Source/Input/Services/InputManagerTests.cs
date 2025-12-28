using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Managers;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;

namespace ChaosWarlords.Tests.Input.Services
{
    [TestClass]
    public class InputManagerTests
    {
        private IInputProvider _mockProvider = null!;
        private InputManager _inputManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockProvider = Substitute.For<IInputProvider>();
            _inputManager = new InputManager(_mockProvider);
        }

        [TestMethod]
        public void Update_UpdatesKeyboardState()
        {
            // Arrange
            var keyState = new KeyboardState(Keys.A);
            _mockProvider.GetKeyboardState().Returns(keyState);

            // Act
            _inputManager.Update();

            // Assert
            _mockProvider.Received(1).GetKeyboardState();
        }

        [TestMethod]
        public void Update_UpdatesMouseState()
        {
            // Arrange
            var mouseState = new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(mouseState);

            // Act
            _inputManager.Update();

            // Assert
            _mockProvider.Received(1).GetMouseState();
        }

        [TestMethod]
        public void IsKeyJustPressed_ReturnsTrueOnRisingEdge()
        {
            // Arrange - Key not pressed initially
            var initialState = new KeyboardState();
            _mockProvider.GetKeyboardState().Returns(initialState);
            _inputManager.Update();

            // Act - Key pressed on next frame
            var pressedState = new KeyboardState(Keys.Enter);
            _mockProvider.GetKeyboardState().Returns(pressedState);
            _inputManager.Update();

            // Assert
            Assert.IsTrue(_inputManager.IsKeyJustPressed(Keys.Enter));
        }

        [TestMethod]
        public void IsKeyJustPressed_ReturnsFalseWhenHeld()
        {
            // Arrange - Key pressed
            var pressedState = new KeyboardState(Keys.Enter);
            _mockProvider.GetKeyboardState().Returns(pressedState);
            _inputManager.Update();

            // Act - Key still pressed on next frame
            _inputManager.Update();

            // Assert
            Assert.IsFalse(_inputManager.IsKeyJustPressed(Keys.Enter));
        }

        [TestMethod]
        public void IsKeyDown_ReturnsTrueWhenPressed()
        {
            // Arrange
            var pressedState = new KeyboardState(Keys.Space);
            _mockProvider.GetKeyboardState().Returns(pressedState);

            // Act
            _inputManager.Update();

            // Assert
            Assert.IsTrue(_inputManager.IsKeyDown(Keys.Space));
        }

        [TestMethod]
        public void IsKeyDown_ReturnsFalseWhenNotPressed()
        {
            // Arrange
            var emptyState = new KeyboardState();
            _mockProvider.GetKeyboardState().Returns(emptyState);

            // Act
            _inputManager.Update();

            // Assert
            Assert.IsFalse(_inputManager.IsKeyDown(Keys.Space));
        }

        [TestMethod]
        public void IsLeftMouseJustClicked_ReturnsTrueOnClick()
        {
            // Arrange - Mouse not clicked initially
            var releasedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(releasedState);
            _inputManager.Update();

            // Act - Mouse clicked on next frame
            var clickedState = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(clickedState);
            _inputManager.Update();

            // Assert
            Assert.IsTrue(_inputManager.IsLeftMouseJustClicked());
        }

        [TestMethod]
        public void IsLeftMouseJustClicked_ReturnsFalseWhenHeld()
        {
            // Arrange - Mouse clicked
            var clickedState = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(clickedState);
            _inputManager.Update();

            // Act - Mouse still clicked on next frame
            _inputManager.Update();

            // Assert
            Assert.IsFalse(_inputManager.IsLeftMouseJustClicked());
        }

        [TestMethod]
        public void IsRightMouseJustClicked_ReturnsTrueOnClick()
        {
            // Arrange - Mouse not clicked initially
            var releasedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(releasedState);
            _inputManager.Update();

            // Act - Right mouse clicked on next frame
            var clickedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(clickedState);
            _inputManager.Update();

            // Assert
            Assert.IsTrue(_inputManager.IsRightMouseJustClicked());
        }

        [TestMethod]
        public void IsMouseOver_ReturnsTrueWhenInside()
        {
            // Arrange
            var mouseState = new MouseState(150, 150, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(mouseState);
            _inputManager.Update();

            var rect = new Rectangle(100, 100, 100, 100);

            // Act & Assert
            Assert.IsTrue(_inputManager.IsMouseOver(rect));
        }

        [TestMethod]
        public void IsMouseOver_ReturnsFalseWhenOutside()
        {
            // Arrange
            var mouseState = new MouseState(50, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(mouseState);
            _inputManager.Update();

            var rect = new Rectangle(100, 100, 100, 100);

            // Act & Assert
            Assert.IsFalse(_inputManager.IsMouseOver(rect));
        }

        [TestMethod]
        public void MousePosition_ReturnsCorrectPosition()
        {
            // Arrange
            var mouseState = new MouseState(250, 350, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(mouseState);

            // Act
            _inputManager.Update();

            // Assert
            Assert.AreEqual(new Vector2(250, 350), _inputManager.MousePosition);
        }
    }
}


