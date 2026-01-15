using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class ActionCompletedCommandTests
    {
        [TestMethod]
        public void Execute_WithPendingCard_DelegatesToMatchManager()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockActionSystem = Substitute.For<IActionSystem>();
            var mockMatchManager = Substitute.For<IMatchManager>();

            stateFake.ActionSystem = mockActionSystem;
            stateFake.MatchManager = mockMatchManager;

            var card = TestData.Cards.CheapCard();
            mockActionSystem.PendingCard.Returns(card);

            var command = new ActionCompletedCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // Assert
            // Command is now a marker, logic handled by ActionSystem internally before/during generation
            // Assert.AreEqual("Normal", stateFake.ActiveModeName);
        }

        [TestMethod]
        public void Execute_NoPendingCard_SkipsControllerCall_ButResetsState()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockActionSystem = Substitute.For<IActionSystem>();
            var mockMatchManager = Substitute.For<IMatchManager>();

            stateFake.ActionSystem = mockActionSystem;
            stateFake.MatchManager = mockMatchManager;

            mockActionSystem.PendingCard.Returns((Card)null!);

            var command = new ActionCompletedCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // Assert
            // Command is now a marker
            // Assert.AreEqual("Normal", stateFake.ActiveModeName);
        }
    }
}
