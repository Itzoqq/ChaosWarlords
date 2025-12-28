using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class CancelActionCommandTests
    {
        [TestMethod]
        public void Execute_CancelsTargetingAndSwitchesToNormalMode()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockState.ActionSystem.Returns(mockActionSystem);
            var command = new CancelActionCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockActionSystem.Received(1).CancelTargeting();
            mockState.Received(1).SwitchToNormalMode();
        }
    }
}


