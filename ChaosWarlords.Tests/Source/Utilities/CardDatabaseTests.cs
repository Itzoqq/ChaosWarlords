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

    // [TestInitialize] is removed because 'new CardDatabase()' inside tests
    // automatically gives us a clean slate every time. No ClearCache needed!

    [TestMethod]
    public void LoadFromJson_CreatesCardsViaFactory()
    {
      // Arrange
      // FIX: Create an instance instead of using static methods
      var db = new CardDatabase();

      // Act
      db.LoadFromJson(MockCardJson);
      var marketCards = db.GetAllMarketCards();

      // Assert
      Assert.HasCount(2, marketCards, "Should create a card for each entry in the JSON.");

      // FIX: Use StartsWith because factory appends GUIDs
      Assert.IsNotNull(marketCards.FirstOrDefault(c => c.Id.StartsWith("noble")), "Noble card should be created.");
      Assert.IsNotNull(marketCards.FirstOrDefault(c => c.Id.StartsWith("soldier")), "Soldier card should be created.");
    }

    [TestMethod]
    public void GetAllMarketCards_ReturnsEmptyList_WhenCacheIsEmpty()
    {
      // Arrange
      // FIX: Create a fresh instance
      var db = new CardDatabase();

      // Act: Don't load anything
      var marketCards = db.GetAllMarketCards();

      // Assert
      Assert.IsNotNull(marketCards);
      Assert.IsEmpty(marketCards, "Should return an empty list if the database was not loaded.");
    }
  }
}