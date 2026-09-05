using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class CardDtoTests
    {
        [TestMethod]
        public void Constructor_WithValidCard_PreservesDefinitionId()
        {
            // Arrange
            var card = TestData.Cards.PowerCard();

            // Act
            var dto = new CardDto(card, 0);

            // Assert
            Assert.AreEqual(card.Id, dto.DefinitionId);
        }

        [TestMethod]
        public void Constructor_WithValidCard_PreservesLocation()
        {
            // Arrange
            var card = new CardBuilder()
                .WithName("test_card")
                .InHand()
                .Build();

            // Act
            var dto = new CardDto(card, 2);

            // Assert
            Assert.AreEqual(CardLocation.Hand, dto.Location);
            Assert.AreEqual(2, dto.ListIndex);
        }

        [TestMethod]
        public void Constructor_WithNullCard_ThrowsArgumentNullException()
        {
            // Act & Assert
            try
            {
                new CardDto(null!, 0);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            }
            catch (ArgumentNullException)
            {
                // Success
            }
        }

        [TestMethod]
        public void ToEntity_WithoutDatabase_ThrowsInvalidOperationException()
        {
            // Arrange
            var dto = new CardDto
            {
                DefinitionId = "test_card",
                Id = "test_card",
                Location = CardLocation.Hand
            };

            // Act & Assert
            try
            {
                dto.ToEntity();
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Success
            }
        }

        [TestMethod]
        public void ToEntity_WithValidDatabase_ReturnsHydratedCard()
        {
            // Arrange
            var mockDatabase = Substitute.For<ICardDatabase>();
            var originalCard = TestData.Cards.PowerCard();
            mockDatabase.GetCardById(originalCard.Id).Returns(originalCard);

            var dto = new CardDto(originalCard, 0);
            dto.Location = CardLocation.DiscardPile;

            // Act
            var result = dto.ToEntity(mockDatabase);

            // Assert
            Assert.AreEqual(originalCard.Id, result.Id);
            Assert.AreEqual(CardLocation.DiscardPile, result.Location);
        }

        [TestMethod]
        public void ToEntity_WithInvalidId_ThrowsInvalidOperationException()
        {
            // Arrange
            var mockDatabase = Substitute.For<ICardDatabase>();
            mockDatabase.GetCardById("invalid_id").Returns((Card?)null);

            var dto = new CardDto
            {
                DefinitionId = "invalid_id",
                Id = "invalid_id"
            };

            // Act & Assert
            try
            {
                dto.ToEntity(mockDatabase);
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Success
            }
        }

        [TestMethod]
        public void Constructor_WithDifferentLocations_PreservesEachLocation()
        {
            // Arrange
            var cardInDeck = new CardBuilder().WithName("deck_card").InDeck().Build();
            var cardInDiscard = new CardBuilder().WithName("discard_card").InDiscard().Build();
            var cardInInnerCircle = new CardBuilder().WithName("inner_card").InInnerCircle().Build();

            // Act
            var deckDto = new CardDto(cardInDeck, 0);
            var discardDto = new CardDto(cardInDiscard, 1);
            var innerDto = new CardDto(cardInInnerCircle, 2);

            // Assert
            Assert.AreEqual(CardLocation.Deck, deckDto.Location);
            Assert.AreEqual(CardLocation.DiscardPile, discardDto.Location);
            Assert.AreEqual(CardLocation.InnerCircle, innerDto.Location);
        }

        [TestMethod]
        public void Constructor_PreservesRuntimeId()
        {
            // RuntimeId must be STABLE across a snapshot-then-restore round trip (it's how a
            // pending command/UI selection still finds "the same card" after a rollback) - so
            // two DTOs snapshotted from the same live card must carry the SAME RuntimeId, not
            // a freshly-generated one each time.
            var card = TestData.Cards.CheapCard();

            var dto1 = new CardDto(card, 0);
            var dto2 = new CardDto(card, 0);

            Assert.AreEqual(card.RuntimeId, dto1.RuntimeId);
            Assert.AreEqual(dto1.RuntimeId, dto2.RuntimeId);
        }

        [TestMethod]
        public void ToEntity_WithValidDatabase_RestoresRuntimeId()
        {
            // Arrange
            var mockDatabase = Substitute.For<ICardDatabase>();
            var originalCard = TestData.Cards.PowerCard();
            mockDatabase.GetCardById(originalCard.DefinitionId).Returns(originalCard);

            var dto = new CardDto(originalCard, 0);

            // Act
            var result = dto.ToEntity(mockDatabase);

            // Assert
            Assert.AreEqual(originalCard.RuntimeId, result.RuntimeId);
        }
    }
}
