using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class WorldBuilderTests
    {
        [TestMethod]
        public void Build_InitializesDependencies_WithoutGraphics()
        {
            // REPLACES: Build_CreatesValidWorldState_WithNullTexture
            // We verify that the builder runs without crashing on a headless environment (no GPU/Textures)
            var builder = new WorldBuilder("dummy_cards.json", "dummy_map.json");
            var world = builder.Build();

            Assert.IsNotNull(world.Player, "Player should be initialized");
            Assert.IsNotNull(world.MapManager, "MapManager should be initialized");
            Assert.IsNotNull(world.MarketManager, "MarketManager should be initialized");
            Assert.IsNotNull(world.ActionSystem, "ActionSystem should be initialized");
        }

        [TestMethod]
        public void Build_FallsBackToTestMap_WhenFilesAreMissing()
        {
            // Arrange
            // We pass garbage paths. TitleContainer.OpenStream will throw FileNotFoundException (or similar).
            // The builder must catch this and load the default TestMap.
            var builder = new WorldBuilder("invalid_cards.json", "invalid_map.json");

            // Act
            var world = builder.Build();

            // Assert
            // The Test Map (hardcoded in MapFactory) has exactly 3 nodes.
            Assert.HasCount(3, world.MapManager.Nodes, "Should load the default Test Map (3 nodes) on file error.");
        }

        [TestMethod]
        public void Build_InitializesPlayerDeck()
        {
            // RESTORED: This test is still valid and important.
            var builder = new WorldBuilder("dummy_cards.json", "dummy_map.json");
            var world = builder.Build();

            // 10 cards total (7 Nobles + 3 Soldiers)
            // 5 in hand, 5 in deck
            Assert.HasCount(5, world.Player.Hand);
            Assert.HasCount(5, world.Player.Deck);
            Assert.AreEqual(10, world.Player.Hand.Count + world.Player.Deck.Count);
        }
    }
}