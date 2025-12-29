using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                InstanceId = Guid.NewGuid().ToString(),
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
                InstanceId = Guid.NewGuid().ToString() 
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
        public void Constructor_GeneratesUniqueInstanceIds()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();
            
            // Act
            var dto1 = new CardDto(card, 0);
            var dto2 = new CardDto(card, 0);
            
            // Assert
            Assert.AreNotEqual(dto1.InstanceId, dto2.InstanceId);
            Assert.IsFalse(string.IsNullOrEmpty(dto1.InstanceId));
            Assert.IsFalse(string.IsNullOrEmpty(dto2.InstanceId));
        }
    }
}
