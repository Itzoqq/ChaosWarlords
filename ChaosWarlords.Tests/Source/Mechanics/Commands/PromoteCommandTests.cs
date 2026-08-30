using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class PromoteCommandTests
    {
        private TestGameplayState _state = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
        }

        [TestMethod]
        public void Validate_Returns_False_When_CardNotInHandOrPlayed()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand("missing_card");

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Validate_Returns_True_When_CardInHand()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Validate_Returns_True_When_CardInPlayedCards()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InPlayed().Build();
            var player = TestData.Players.RedPlayer();
            player.AddToPlayed(card);
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Validate_DoesNotMutateState_CardRemainsInHandAfterValidation()
        {
            // Arrange: regression test - Validate() used to call TryPromoteCard() directly, which
            // actually moved the card to the Inner Circle as a side effect of "checking". Since
            // CommandDispatcher calls Validate() then Execute() on the same instance, that left
            // Execute()'s own promotion call unable to find the card at all. Validate() must be a
            // pure read (see IGameCommand.Validate's contract).
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            command.Validate(_state.MatchContext);

            // Assert
            CollectionAssert.Contains(player.Hand.ToList(), card, "Validate() must not remove the card from Hand");
            CollectionAssert.DoesNotContain(player.InnerCircle.ToList(), card, "Validate() must not promote the card");
        }

        [TestMethod]
        public void Execute_MovesCardToInnerCircle_AndRecordsAction_WhenCardInHand()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            CollectionAssert.DoesNotContain(player.Hand.ToList(), card);
            CollectionAssert.Contains(player.InnerCircle.ToList(), card);
        }

        [TestMethod]
        public void Execute_FollowingValidate_StillPromotesTheCard()
        {
            // Arrange: exercises the same Validate()-then-Execute() sequence CommandDispatcher
            // uses in production, guarding against the double-invocation bug described above.
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            var isValid = command.Validate(_state.MatchContext);
            command.Execute(_state.MatchContext);

            // Assert
            Assert.IsTrue(isValid);
            CollectionAssert.Contains(player.InnerCircle.ToList(), card);
        }

        [TestMethod]
        public void Execute_DoesNothing_When_CardNotFound()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand("missing_card");

            // Act & Assert: should not throw
            command.Execute(_state.MatchContext);

            Assert.IsEmpty(player.InnerCircle);
        }
    }
}
