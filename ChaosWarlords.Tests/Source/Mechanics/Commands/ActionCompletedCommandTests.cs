using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
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
            command.Execute(stateFake);

            // Assert
            // 1. Verify logic was delegated to the Controller
            mockMatchManager.Received(1).PlayCard(card);

            // 2. Verify cleanup
            mockActionSystem.Received(1).CancelTargeting();
            
            // 3. Verify Mode Switch via State Property
            Assert.AreEqual("Normal", stateFake.ActiveModeName);
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
            command.Execute(stateFake);

            // Assert
            // 1. Verify Controller was NOT called
            mockMatchManager.DidNotReceive().PlayCard(Arg.Any<Card>());

            // 2. Verify cleanup still happens
            mockActionSystem.Received(1).CancelTargeting();
            
            // 3. Verify Mode Switch via State Property
            // Note: Command calls SwitchToNormalMode()
            Assert.AreEqual("Normal", stateFake.ActiveModeName);
        }
    }
}
