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
            command.Execute(stateFake);

            // Assert
            Assert.IsTrue(stateFake.EndTurnCalled, "EndTurn should be called when allowed.");
        }

        [TestMethod]
        public void Execute_WhenCannotEndTurn_DoesNotCallEndTurn()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            stateFake.TestCanEndTurnResult = false;
            
            var command = new EndTurnCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            Assert.IsFalse(stateFake.EndTurnCalled, "EndTurn should NOT be called when prohibited.");
        }
    }
}
