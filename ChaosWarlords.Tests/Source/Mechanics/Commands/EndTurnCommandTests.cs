using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]

    [TestCategory("Unit")]
    public class EndTurnCommandTests
    {
        [TestMethod]
        public void Execute_WhenCanEndTurn_CallsEndTurn()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            mockState.CanEndTurn(out Arg.Any<string>()).Returns(true);
            var command = new EndTurnCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).EndTurn();
        }

        [TestMethod]
        public void Execute_WhenCannotEndTurn_DoesNotCallEndTurn()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            mockState.CanEndTurn(out Arg.Any<string>()).Returns(false);
            var command = new EndTurnCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockState.DidNotReceive().EndTurn();
        }
    }
}


