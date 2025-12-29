using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Managers;
using Microsoft.Xna.Framework;
using NSubstitute;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]

    [TestCategory("Unit")]
    public class UIManagerTests
    {
        private UIManager _ui = null!;
        private IInputManager _input = null!;
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
            // Use the new helper to create mouse state
            var mouseState = InputTestHelpers.CreateReleasedMouseState(x, y);

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
            // Ensure pause is false (default, but explicit is good)
            _ui.IsPaused = false;

            SetMouse(770, 200);
            _ui.Update(_input);

            Assert.IsTrue(_ui.IsAssassinateHovered);
            Assert.IsFalse(_ui.IsReturnSpyHovered);
            Assert.IsFalse(_ui.IsMarketHovered);
        }


        [TestMethod]
        public void PauseMenu_Layout_IsCalculated()
        {
            // Verify Rects are not empty
            Assert.AreNotEqual(Rectangle.Empty, _ui.PauseMenuBackgroundRect);
            Assert.AreNotEqual(Rectangle.Empty, _ui.ResumeButtonRect);
            Assert.AreNotEqual(Rectangle.Empty, _ui.MainMenuButtonRect);
            Assert.AreNotEqual(Rectangle.Empty, _ui.ExitButtonRect);
        }

        [TestMethod]
        public void PauseMenu_Hover_IsDetected()
        {
            // Resume button is top button. 
            // We need to know where it is exactly or just get the Center.
            // Also need to set IsPaused = true for it to be active!
            _ui.IsPaused = true;

            var rect = _ui.ResumeButtonRect;
            SetMouse(rect.Center.X, rect.Center.Y);

            _ui.Update(_input);

            Assert.IsTrue(_ui.IsResumeHovered);
            Assert.IsFalse(_ui.IsExitHovered);
        }
    }
}

