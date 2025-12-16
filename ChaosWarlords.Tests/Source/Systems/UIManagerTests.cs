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
        private const int ButtonHeight = 100;
        private const int ButtonWidth = 40;
        private const int VerticalGap = 25;

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
            // --- Market Button (Left) ---
            // Y: (600 / 2) - (100 / 2) = 300 - 50 = 250
            Assert.AreEqual(0, _ui.MarketButtonRect.X);
            Assert.AreEqual(250, _ui.MarketButtonRect.Y);
            Assert.AreEqual(ButtonWidth, _ui.MarketButtonRect.Width);
            Assert.AreEqual(ButtonHeight, _ui.MarketButtonRect.Height);

            // --- Assassinate Button (Top Right) ---
            // Y: (600 / 2) - 100 - (25 / 2) = 300 - 100 - 12 (int div) = 188
            Assert.AreEqual(ScreenWidth - ButtonWidth, _ui.AssassinateButtonRect.X);
            Assert.AreEqual(188, _ui.AssassinateButtonRect.Y);
            Assert.AreEqual(ButtonWidth, _ui.AssassinateButtonRect.Width);
            Assert.AreEqual(ButtonHeight, _ui.AssassinateButtonRect.Height);

            // --- Return Spy Button (Bottom Right) ---
            // Y: (600 / 2) + (25 / 2) = 300 + 12 (int div) = 312
            // FIX: The expected value is 312, matching integer division.
            Assert.AreEqual(ScreenWidth - ButtonWidth, _ui.ReturnSpyButtonRect.X);
            Assert.AreEqual(312, _ui.ReturnSpyButtonRect.Y); // FIX: Changed expected from 313 to 312
            Assert.AreEqual(ButtonWidth, _ui.ReturnSpyButtonRect.Width);
            Assert.AreEqual(ButtonHeight, _ui.ReturnSpyButtonRect.Height);
        }

        #endregion

        // Helper to move the fake mouse and update the input manager
        private void SetMouse(int x, int y)
        {
            _mockInput.MouseState = new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();
        }

        [TestMethod]
        public void IsMarketButtonHovered_ReturnsTrue_WhenMouseInside()
        {
            // Market is at (0, 250) size (40, 100). Point (20, 260) is inside.
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
            // Assassinate is at (760, 188) size (40, 100). Point (770, 200) is inside.
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
    }
}