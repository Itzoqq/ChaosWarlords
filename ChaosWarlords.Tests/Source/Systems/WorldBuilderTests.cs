using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class WorldBuilderTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            // Arrange
            // Create the mock dependency
            var mockDb = new MockCardDatabase();

            // Pass the mock DB instead of a string path for cards
            var builder = new WorldBuilder(mockDb, "dummy_map.json");

            // Act
            var world = builder.Build();

            // Assert
            // Check TurnManager.ActivePlayer instead of world.Player
            Assert.IsNotNull(world.TurnManager.ActivePlayer, "ActivePlayer should be initialized via TurnManager");
            Assert.IsNotNull(world.MapManager, "MapManager should be initialized");
            Assert.IsNotNull(world.MarketManager, "MarketManager should be initialized");
            Assert.IsNotNull(world.ActionSystem, "ActionSystem should be initialized");
        }

        [TestMethod]
        public void Build_InitializesPlayerDeck()
        {
            // Use the mock
            var mockDb = new MockCardDatabase();
            var builder = new WorldBuilder(mockDb, "dummy_map.json");

            var world = builder.Build();

            // 10 cards total (7 Nobles + 3 Soldiers)
            // The WorldBuilder is now only responsible for creating the full 10-card deck.
            // The initial hand draw is handled later in GameplayState.LoadContent().

            // Assert Hand is empty and Deck is full.
            Assert.HasCount(0, world.TurnManager.ActivePlayer.Hand, "Hand should be empty immediately after WorldBuilder.Build()");
            Assert.HasCount(10, world.TurnManager.ActivePlayer.Deck, "Deck should contain all 10 starting cards.");
            Assert.AreEqual(10, world.TurnManager.ActivePlayer.Hand.Count + world.TurnManager.ActivePlayer.Deck.Count, "Total cards in deck and hand must sum to 10.");
        }

        [TestMethod]
        public void Build_FallsBackToTestMap_WhenFilesAreMissing()
        {
            var mockDb = new MockCardDatabase();

            // We pass a garbage map path to force the fallback
            var builder = new WorldBuilder(mockDb, "invalid_map.json");

            var world = builder.Build();

            // The Test Map (hardcoded in MapFactory) has exactly 3 nodes.
            Assert.HasCount(3, world.MapManager.NodesInternal, "Should load the default Test Map (3 nodes) on file error.");
        }
    }
}