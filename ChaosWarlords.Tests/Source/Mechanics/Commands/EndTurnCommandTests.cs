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

        [TestMethod]
        public void Validate_WhenNotTargeting_ReturnsTrue()
        {
            // Per the rules a player may end their turn early at any time - see planning.txt.
            var stateFake = new TestGameplayState();
            stateFake.ActionSystem.IsTargeting().Returns(false);

            var command = new EndTurnCommand();

            Assert.IsTrue(command.Validate(stateFake.MatchContext));
        }

        [TestMethod]
        public void Validate_WhileTargeting_ReturnsFalse()
        {
            // The actual authoritative gate against ending the turn out from under an
            // in-progress targeting sequence (including a deferred "up to N" promotion
            // redemption or a forced-discard flow) - independent of any UI-layer check, so it
            // holds even for a caller that bypasses the UI entirely (AI, network client,
            // replay). See planning.txt for the concrete repro this closes.
            var stateFake = new TestGameplayState();
            stateFake.ActionSystem.IsTargeting().Returns(true);

            var command = new EndTurnCommand();

            Assert.IsFalse(command.Validate(stateFake.MatchContext));
        }
    }
}
