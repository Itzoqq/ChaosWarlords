using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using System;

namespace ChaosWarlords.Tests.Integration.Input.Services
{
    [TestClass]
    [TestCategory("Integration")]
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
        public void Update_FiresLeftClickEvent_OnRisingEdge()
        {
            // Arrange - Listen for event
            bool eventFired = false;
            _inputManager.OnInputEvent += (s, e) => 
            {
                if (e.Type == InputEventType.LeftClick) eventFired = true;
            };

            // Mouse not clicked initially
            var releasedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(releasedState);
            _inputManager.Update();

            // Act - Mouse clicked on next frame
            var clickedState = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(clickedState);
            _inputManager.Update();

            // Assert
            Assert.IsTrue(eventFired, "LeftClick event should have fired on rising edge.");
        }

        [TestMethod]
        public void Update_DoesNotFireLeftClick_WhenHeld()
        {
            // Arrange
            int eventCount = 0;
            _inputManager.OnInputEvent += (s, e) => { if (e.Type == InputEventType.LeftClick) eventCount++; };

            // Mouse clicked
            var clickedState = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(clickedState);
            _inputManager.Update(); 
            // Reset count if it fired for the first update (it might depend on initial state, but typically previous is default/empty)
            // Initial state of InputManager uses default structs (Released).
            // So first update WILL fire if we start pressed. 
            // Let's assume we want to test "Held" meaning 2nd frame.
            
            eventCount = 0; // Reset

            // Act - Mouse still clicked on next frame
            _inputManager.Update();

            // Assert
            Assert.AreEqual(0, eventCount, "LeftClick event should NOT fire when button is held.");
        }

        [TestMethod]
        public void Update_FiresRightClickEvent_OnRisingEdge()
        {
            // Arrange
            bool eventFired = false;
            _inputManager.OnInputEvent += (s, e) => 
            {
                if (e.Type == InputEventType.RightClick) eventFired = true;
            };

            var releasedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(releasedState);
            _inputManager.Update();

            // Act
            var clickedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _mockProvider.GetMouseState().Returns(clickedState);
            _inputManager.Update();

            // Assert
            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void Update_FiresKeyDownEvent_OnRisingEdge()
        {
            // Arrange
            bool eventFired = false;
            _inputManager.OnInputEvent += (s, e) => 
            {
                if (e.Type == InputEventType.KeyDown && e.Key == Keys.Enter) eventFired = true;
            };

            var initialState = new KeyboardState();
            _mockProvider.GetKeyboardState().Returns(initialState);
            _inputManager.Update();

            // Act
            var pressedState = new KeyboardState(Keys.Enter);
            _mockProvider.GetKeyboardState().Returns(pressedState);
            _inputManager.Update();

            // Assert
            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void Update_DoesNotFireKeyDown_WhenHeld()
        {
            // Arrange
            int eventCount = 0;
            _inputManager.OnInputEvent += (s, e) => { if (e.Type == InputEventType.KeyDown) eventCount++; };

            var pressedState = new KeyboardState(Keys.Enter);
            _mockProvider.GetKeyboardState().Returns(pressedState);
            _inputManager.Update();
            
            eventCount = 0; // Reset (first update fired it)

            // Act
            _inputManager.Update();

            // Assert
            Assert.AreEqual(0, eventCount);
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


