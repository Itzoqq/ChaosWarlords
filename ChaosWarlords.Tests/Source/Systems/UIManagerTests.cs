using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Tests.Systems
{
    // Reusing the Mock concept since we can't use real mouse hardware in tests
    public class MockUIInputProvider : IInputProvider
    {
        public MouseState MouseState;
        public KeyboardState KeyboardState; // Not used here, but required by interface

        public MouseState GetMouseState() => MouseState;
        public KeyboardState GetKeyboardState() => KeyboardState;
    }

    [TestClass]
    public class UIManagerTests
    {
        private UIManager _ui = null!;
        private InputManager _input = null!;
        private MockUIInputProvider _mockInput = null!;

        // Standard Resolution for testing math
        private const int ScreenWidth = 800;
        private const int ScreenHeight = 600;

        [TestInitialize]
        public void Setup()
        {
            // 1. Create the UI Manager with specific dimensions
            _ui = new UIManager(ScreenWidth, ScreenHeight);

            // 2. Setup the mock input
            _mockInput = new MockUIInputProvider();
            _input = new InputManager(_mockInput);
        }

        #region Layout Tests

        [TestMethod]
        public void Constructor_CalculatesLayout_Correctly()
        {
            // Verify Market Button (Left Center)
            // Screen Height 600 / 2 = 300. Button Height 100 / 2 = 50.
            // Expected Y = 250.
            Assert.AreEqual(0, _ui.MarketButtonRect.X);
            Assert.AreEqual(250, _ui.MarketButtonRect.Y);
            Assert.AreEqual(40, _ui.MarketButtonRect.Width);
            Assert.AreEqual(100, _ui.MarketButtonRect.Height);

            // Verify Assassinate Button (Right Side, Shifted Up)
            // Screen Width 800 - 40 = 760 X.
            // Center (300) - Height (100) - Gap (25) = 175 Y.
            Assert.AreEqual(760, _ui.AssassinateButtonRect.X);
            Assert.AreEqual(175, _ui.AssassinateButtonRect.Y);
        }

        [TestMethod]
        public void RecalculateLayout_UpdatesPositions_WhenScreenResizes()
        {
            // Arrange: Change "Screen" size to 4K
            _ui = new UIManager(3840, 2160);

            // Act: Logic runs in constructor, but let's verify positions changed
            // Right buttons should be way over at X = 3800
            Assert.AreEqual(3800, _ui.AssassinateButtonRect.X);
            // Center Y should be 1080 approx
            Assert.IsGreaterThan(1000, _ui.MarketButtonRect.Y);
        }

        #endregion

        #region Interaction Tests

        [TestMethod]
        public void IsMarketButtonHovered_ReturnsTrue_WhenMouseInside()
        {
            // Market button is at (0, 250) with size (40, 100)
            // Point (20, 260) is definitely inside.
            SetMouse(20, 260);

            Assert.IsTrue(_ui.IsMarketButtonHovered(_input));
        }

        [TestMethod]
        public void IsMarketButtonHovered_ReturnsFalse_WhenMouseOutside()
        {
            // Point (50, 260) is to the right of the button (Width is 40)
            SetMouse(50, 260);

            Assert.IsFalse(_ui.IsMarketButtonHovered(_input));
        }

        [TestMethod]
        public void IsAssassinateButtonHovered_ReturnsTrue_WhenMouseInside()
        {
            // Assassinate is at (760, 175) size (40, 100)
            SetMouse(770, 200);

            Assert.IsTrue(_ui.IsAssassinateButtonHovered(_input));
        }

        [TestMethod]
        public void Buttons_DoNotOverlap_Logically()
        {
            // Verify that hovering one button doesn't trigger another
            SetMouse(770, 200); // Inside Assassinate

            Assert.IsTrue(_ui.IsAssassinateButtonHovered(_input));
            Assert.IsFalse(_ui.IsReturnSpyButtonHovered(_input));
            Assert.IsFalse(_ui.IsMarketButtonHovered(_input));
        }

        #endregion

        // Helper to move the fake mouse and update the input manager
        private void SetMouse(int x, int y)
        {
            _mockInput.MouseState = new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();
        }
    }
}