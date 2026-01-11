using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

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
            var stateFake = new TestGameplayState();
            stateFake.TestCanEndTurnResult = true;
            
            var command = new EndTurnCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // Command delegates to MatchManager
            stateFake.MatchManager.Received(1).EndTurn();
        }
    }
}
