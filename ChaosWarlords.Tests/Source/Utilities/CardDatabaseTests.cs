using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Utilities
{
    [TestClass]
    public class CardDatabaseTests
    {
        private const string MockCardJson = @"
        [
          {
            ""id"": ""noble"",
            ""name"": ""Noble"",
            ""description"": ""A starting card."",
            ""cost"": 0,
            ""aspect"": ""Neutral"",
            ""deckVP"": 1,
            ""innerCircleVP"": 0,
            ""effects"": [
              { ""type"": ""GainResource"", ""amount"": 1, ""targetResource"": ""Influence"" }
            ]
          },
          {
            ""id"": ""soldier"",
            ""name"": ""Soldier"",
            ""description"": ""A starting card."",
            ""cost"": 0,
            ""aspect"": ""Neutral"",
            ""deckVP"": 0,
            ""innerCircleVP"": 0,
            ""effects"": [
              { ""type"": ""GainResource"", ""amount"": 1, ""targetResource"": ""Power"" }
            ]
          }
        ]";

        [TestInitialize]
        public void Setup()
        {
            // Clear the static cache before each test to ensure isolation
            CardDatabase.ClearCache();
        }

        [TestMethod]
        public void LoadFromJson_CreatesCardsViaFactory()
        {
            // Arrange
            CardDatabase.LoadFromJson(MockCardJson);

            // Act
            var marketCards = CardDatabase.GetAllMarketCards();

            // Assert
            Assert.HasCount(2, marketCards, "Should create a card for each entry in the JSON.");
            Assert.IsNotNull(marketCards.FirstOrDefault(c => c.Id == "noble"), "Noble card should be created.");
            Assert.IsNotNull(marketCards.FirstOrDefault(c => c.Id == "soldier"), "Soldier card should be created.");
        }

        [TestMethod]
        public void GetAllMarketCards_ReturnsEmptyList_WhenCacheIsEmpty()
        {
            // Act: Don't load anything
            var marketCards = CardDatabase.GetAllMarketCards();

            // Assert
            Assert.IsEmpty(marketCards, "Should return an empty list if the database was not loaded.");
        }
    }
}