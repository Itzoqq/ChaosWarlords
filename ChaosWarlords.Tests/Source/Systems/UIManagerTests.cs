using ChaosWarlords.Source.Systems;
using Microsoft.Xna.Framework.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class UIManagerTests
    {
        private UIManager _ui = null!;
        private InputManager _input = null!;
        private IInputProvider _inputProvider = null!;

        private const int ScreenWidth = 800;
        private const int ScreenHeight = 600;

        [TestInitialize]
        public void Setup()
        {
            _ui = new UIManager(ScreenWidth, ScreenHeight);

            // Create the NSubstitute mock
            _inputProvider = Substitute.For<IInputProvider>();

            // Inject the mock into the concrete InputManager
            _input = new InputManager(_inputProvider);
        }

        private void SetMouse(int x, int y)
        {
            // Define the state we want the mock to return
            var mouseState = new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

            // Configure the mock
            _inputProvider.GetMouseState().Returns(mouseState);

            // Update InputManager so it fetches the new state from our mock
            _input.Update();
        }

        [TestMethod]
        public void IsMarketHovered_ReturnsTrue_WhenMouseInside()
        {
            // Market is at (0, 250) size (40, 100). Point (20, 260) is inside.
            SetMouse(20, 260);

            // ACTION: Must call Update to calculate hovers
            _ui.Update(_input);

            // ASSERT: Check the property
            Assert.IsTrue(_ui.IsMarketHovered);
        }

        [TestMethod]
        public void IsMarketHovered_ReturnsFalse_WhenMouseOutside()
        {
            SetMouse(50, 260);

            _ui.Update(_input); // Calculate

            Assert.IsFalse(_ui.IsMarketHovered);
        }

        [TestMethod]
        public void IsAssassinateHovered_ReturnsTrue_WhenMouseInside()
        {
            // Assassinate is at (760, 188). Point (770, 200) is inside.
            SetMouse(770, 200);

            _ui.Update(_input);

            Assert.IsTrue(_ui.IsAssassinateHovered);
        }

        [TestMethod]
        public void Buttons_DoNotOverlap_Logically()
        {
            // 1. Hover Assassinate
            SetMouse(770, 200);
            _ui.Update(_input);

            Assert.IsTrue(_ui.IsAssassinateHovered);
            Assert.IsFalse(_ui.IsReturnSpyHovered);
            Assert.IsFalse(_ui.IsMarketHovered);
        }
    }
}