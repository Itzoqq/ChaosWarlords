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

            var mockActionSystem = stateFake.ActionSystem;
            // Verify TryStartAssassinate sets state (simulated)
            mockActionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);

            var command = new StartAssassinateCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // 1. Verify Action delegation
            mockActionSystem.Received(1).StartTargeting(Arg.Is(ActionState.TargetingAssassinate), null);

            // 2. Verify State Transition
            // Assert.AreEqual("Targeting", stateFake.ActiveModeName, "Should switch to Targeting mode.");
        }
    }
}
