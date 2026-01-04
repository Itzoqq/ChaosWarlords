using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class StartAssassinateCommandTests
    {
        [TestMethod]
        public void Execute_StartsAssassinateAndSwitchesToTargetingMode()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            
            var mockActionSystem = Substitute.For<IActionSystem>();
            // Verify TryStartAssassinate sets state (simulated)
            mockActionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            stateFake.ActionSystem = mockActionSystem;
            
            var command = new StartAssassinateCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            // 1. Verify Action delegation
            mockActionSystem.Received(1).TryStartAssassinate();
            
            // 2. Verify State Transition
            Assert.AreEqual("Targeting", stateFake.ActiveModeName, "Should switch to Targeting mode.");
        }
    }
}
