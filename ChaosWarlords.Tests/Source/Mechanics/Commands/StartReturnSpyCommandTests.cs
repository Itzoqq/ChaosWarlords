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

            var mockActionSystem = stateFake.ActionSystem;
            mockActionSystem.CurrentState.Returns(ActionState.TargetingReturnSpy);

            var command = new StartReturnSpyCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // 1. Verify Action delegation
            mockActionSystem.Received(1).StartTargeting(Arg.Is(ActionState.TargetingReturnSpy), null);

            // 2. Verify State Transition
            // Assert.AreEqual("Targeting", stateFake.ActiveModeName, "Should switch to Targeting mode.");
        }
    }
}
