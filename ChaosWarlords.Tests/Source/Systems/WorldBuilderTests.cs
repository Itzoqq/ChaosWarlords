using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class WorldBuilderTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_WithNullTexture()
        {
            // Arrange
            // We pass non-existent paths so it falls back to defaults/test map
            var builder = new WorldBuilder("dummy_cards.json", "dummy_map.json");

            // Act
            // Passing null for texture to verify headless support
            var world = builder.Build();

            // Assert
            Assert.IsNotNull(world.Player, "Player should be initialized");
            Assert.IsNotNull(world.MapManager, "MapManager should be initialized");
            Assert.IsNotNull(world.MarketManager, "MarketManager should be initialized");
            Assert.IsNotNull(world.ActionSystem, "ActionSystem should be initialized");
        }

        [TestMethod]
        public void Build_InitializesPlayerDeck()
        {
            var builder = new WorldBuilder("dummy_cards.json", "dummy_map.json");
            var world = builder.Build();

            // 10 cards total (7 Nobles + 3 Soldiers)
            // 5 in hand, 5 in deck
            Assert.HasCount(5, world.Player.Hand);
            Assert.HasCount(5, world.Player.Deck);
            Assert.AreEqual(10, world.Player.Hand.Count + world.Player.Deck.Count);
        }

        [TestMethod]
        public void Build_FallsBackToTestMap_WhenFileMissing()
        {
            var builder = new WorldBuilder("missing.json", "missing.json");
            var world = builder.Build();

            // TestMap has 3 nodes
            Assert.HasCount(3, world.MapManager.Nodes);
        }
    }
}