using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Utilities
{
  [TestClass]

  [TestCategory("Unit")]
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
      // Create an instance instead of using static methods
      var db = new CardDatabase();

      // Act
      db.LoadFromJson(MockCardJson);
      var marketCards = db.GetAllMarketCards();

      // Assert
      Assert.HasCount(2, marketCards, "Should create a card for each entry in the JSON.");

      // Use StartsWith because factory appends GUIDs
      Assert.IsNotNull(marketCards.FirstOrDefault(c => c.Id.StartsWith("noble")), "Noble card should be created.");
      Assert.IsNotNull(marketCards.FirstOrDefault(c => c.Id.StartsWith("soldier")), "Soldier card should be created.");
    }

    [TestMethod]
    public void GetAllMarketCards_ReturnsEmptyList_WhenCacheIsEmpty()
    {
      // Arrange
      // Create a fresh instance
      var db = new CardDatabase();

      // Act: Don't load anything
      var marketCards = db.GetAllMarketCards();

      // Assert
      Assert.IsNotNull(marketCards);
      Assert.IsEmpty(marketCards, "Should return an empty list if the database was not loaded.");
    }
    [TestMethod]
    public void GetCardById_ReturnsCard_WhenIdExists()
    {
      // Arrange
      var db = new CardDatabase();
      db.LoadFromJson(MockCardJson);

      // Act
      var card = db.GetCardById("noble");

      // Assert
      Assert.IsNotNull(card);
      Assert.StartsWith("noble", card.Id); // Factory appends GUID
      Assert.AreEqual("Noble", card.Name);
    }

    [TestMethod]
    public void GetCardById_ReturnsNull_WhenIdDoesNotExist()
    {
      // Arrange
      var db = new CardDatabase();
      db.LoadFromJson(MockCardJson);

      // Act
      var card = db.GetCardById("nonExistentId");

      // Assert
      Assert.IsNull(card);
    }

    [TestMethod]
    public void Load_ReadsFromStream_AndPopulatesCache()
    {
      // Arrange
      var db = new CardDatabase();
      var json = MockCardJson;
      using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
      {
        // Act
        db.Load(stream);
      }

      // Assert
      var card = db.GetCardById("noble");
      Assert.IsNotNull(card);
      Assert.AreEqual("Noble", card.Name);
    }
    [TestMethod]
    public void LoadFromJson_ParsesRecursiveEffects()
    {
        // Arrange
        string recursiveJson = @"
        [
          {
            ""id"": ""wight"",
            ""name"": ""Wight"",
            ""description"": ""Recursive Test"",
            ""cost"": 3,
            ""aspect"": ""Malice"",
            ""deckVP"": 1,
            ""innerCircleVP"": 3,
            ""effects"": [
              {
                ""type"": ""Devour"",
                ""amount"": 1,
                ""onSuccess"": {
                    ""type"": ""Supplant"",
                    ""amount"": 1
                }
              }
            ]
          }
        ]";

        var db = new CardDatabase();
        db.LoadFromJson(recursiveJson);

        // Act
        var card = db.GetCardById("wight");

        // Assert
        Assert.IsNotNull(card);
        Assert.HasCount(1, card.Effects);
        
        var devourEffect = card.Effects[0];
        Assert.AreEqual(ChaosWarlords.Source.Utilities.EffectType.Devour, devourEffect.Type);
        
        Assert.IsNotNull(devourEffect.OnSuccess, "Recursive effect should be parsed.");
        Assert.AreEqual(ChaosWarlords.Source.Utilities.EffectType.Supplant, devourEffect.OnSuccess.Type);
    }
  }
}

