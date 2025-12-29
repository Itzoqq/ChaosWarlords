using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]

    [TestCategory("Unit")]
    public class ResolveSpyCommandTests
    {
        [TestMethod]
        public void Execute_CallsFinalizeSpyReturnOnActionSystem()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockState.ActionSystem.Returns(mockActionSystem);
            var command = new ResolveSpyCommand(PlayerColor.Blue);

            // Act
            command.Execute(mockState);

            // Assert
            mockActionSystem.Received(1).FinalizeSpyReturn(PlayerColor.Blue);
        }
    }
}



