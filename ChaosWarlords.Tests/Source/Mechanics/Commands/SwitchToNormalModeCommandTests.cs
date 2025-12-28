using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class SwitchToNormalModeCommandTests
    {
        [TestMethod]
        public void Execute_SwitchesToNormalMode()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var command = new SwitchToNormalModeCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).SwitchToNormalMode();
        }
    }
}


