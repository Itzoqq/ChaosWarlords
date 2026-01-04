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
    public class StartReturnSpyCommandTests
    {
        [TestMethod]
        public void Execute_StartsReturnSpyAndSwitchesToTargetingMode()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.TargetingReturnSpy);
            stateFake.ActionSystem = mockActionSystem;
            
            var command = new StartReturnSpyCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            // 1. Verify Action delegation
            mockActionSystem.Received(1).TryStartReturnSpy();

            // 2. Verify State Transition
            Assert.AreEqual("Targeting", stateFake.ActiveModeName, "Should switch to Targeting mode.");
        }
    }
}
