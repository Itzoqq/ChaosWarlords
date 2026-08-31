using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class DevourCardCommandTests
    {
        [TestMethod]
        public void Constructor_StoresCard()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();

            // Act
            var command = new DevourCardCommand(card);

            // Assert
            Assert.IsNotNull(command);
        }

        [TestMethod]
        public void Validate_CardInHand_ReturnsTrue()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            var player = new Player(PlayerColor.Red);
            stateFake.TurnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.CheapCard();
            player.AddToHand(card);
            var command = new DevourCardCommand(card);

            // Act
            bool result = command.Validate(stateFake.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Validate_CardNotFoundAnywhere_ReturnsFalse()
        {
            // Arrange: card was never added to the active player's Hand/InnerCircle/
            // PlayedCards or the market row - e.g. already devoured, or a bogus/stale
            // RuntimeId - so ResolveCard() (the same lookup Execute() itself uses) finds
            // nothing. A server must be able to reject this via Validate() alone, without
            // relying on Execute()'s own silent no-op.
            var stateFake = new TestGameplayState();
            var player = new Player(PlayerColor.Red);
            stateFake.TurnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.CheapCard();
            var command = new DevourCardCommand(card); // never added anywhere

            // Act
            bool result = command.Validate(stateFake.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Execute_CallsMatchManagerDevour()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockMatchManager = stateFake.MatchManager;
            var mockActionSystem = stateFake.ActionSystem;

            var player = new Player(PlayerColor.Red);
            stateFake.TurnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.CheapCard();
            player.AddToHand(card);
            var command = new DevourCardCommand(card);

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            mockMatchManager.Received(1).DevourCard(card);
        }

        [TestMethod]
        public void Execute_CallsActionSystemCompleteAction()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockMatchManager = stateFake.MatchManager;
            var mockActionSystem = stateFake.ActionSystem;

            var player = new Player(PlayerColor.Red);
            stateFake.TurnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.CheapCard();
            player.AddToHand(card);
            var command = new DevourCardCommand(card);

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            mockActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_CallsMethodsInCorrectOrder()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockMatchManager = stateFake.MatchManager;
            var mockActionSystem = stateFake.ActionSystem;

            var player = new Player(PlayerColor.Red);
            stateFake.TurnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.CheapCard();
            player.AddToHand(card);
            var command = new DevourCardCommand(card);

            var callOrder = new System.Collections.Generic.List<string>();

            mockMatchManager.When(x => x.DevourCard(Arg.Any<Card>()))
                .Do(_ => callOrder.Add("DevourCard"));
            mockActionSystem.When(x => x.CompleteAction())
                .Do(_ => callOrder.Add("CompleteAction"));

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            Assert.HasCount(2, callOrder);
            Assert.AreEqual("DevourCard", callOrder[0]);
            Assert.AreEqual("CompleteAction", callOrder[1]);
        }
    }
}
