using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class CancelActionCommandTests
    {
        [TestMethod]
        public void Execute_CancelsTargetingAndSwitchesToNormalMode()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            stateFake.ActiveModeName = "Targeting"; // Start in not-Normal mode

            var mockActionSystem = Substitute.For<IActionSystem>();
            stateFake.ActionSystem = mockActionSystem;
            
            var command = new CancelActionCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            // 1. Verify Action System delegation
            mockActionSystem.Received(1).CancelTargeting();
            
            // 2. Verify State Transition
            Assert.AreEqual("Normal", stateFake.ActiveModeName, "Should switch to Normal mode.");
        }
    }
}
