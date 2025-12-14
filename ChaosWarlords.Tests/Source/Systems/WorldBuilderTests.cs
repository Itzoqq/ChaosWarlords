using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities; // Needed for ICardDatabase interface
using ChaosWarlords.Source.Entities; // Needed for Card class
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    // 1. Create a fake database for testing (So we don't need real files)
    public class MockWorldBuilderDB : ICardDatabase
    {
        public List<Card> GetAllMarketCards()
        {
            // Return an empty list or a list with 1 dummy card if needed.
            // For WorldBuilder tests, empty is usually fine as we test Player Deck separately.
            return new List<Card>();
        }

        public Card? GetCardById(string id) => null;
    }

    [TestClass]
    public class WorldBuilderTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            // Arrange
            // FIX: Create the mock dependency
            var mockDb = new MockWorldBuilderDB();

            // FIX: Pass the mock DB instead of a string path for cards
            var builder = new WorldBuilder(mockDb, "dummy_map.json");

            // Act
            var world = builder.Build();

            // Assert
            // FIX: Check TurnManager.ActivePlayer instead of world.Player
            Assert.IsNotNull(world.TurnManager.ActivePlayer, "ActivePlayer should be initialized via TurnManager");
            Assert.IsNotNull(world.MapManager, "MapManager should be initialized");
            Assert.IsNotNull(world.MarketManager, "MarketManager should be initialized");
            Assert.IsNotNull(world.ActionSystem, "ActionSystem should be initialized");
        }

        [TestMethod]
        public void Build_InitializesPlayerDeck()
        {
            // FIX: Use the mock
            var mockDb = new MockWorldBuilderDB();
            var builder = new WorldBuilder(mockDb, "dummy_map.json");

            var world = builder.Build();

            // 10 cards total (7 Nobles + 3 Soldiers)
            // The WorldBuilder is now only responsible for creating the full 10-card deck.
            // The initial hand draw is handled later in GameplayState.LoadContent().

            // FIX: Assert Hand is empty and Deck is full.
            Assert.HasCount(0, world.TurnManager.ActivePlayer.Hand, "Hand should be empty immediately after WorldBuilder.Build()");
            Assert.HasCount(10, world.TurnManager.ActivePlayer.Deck, "Deck should contain all 10 starting cards.");
            Assert.AreEqual(10, world.TurnManager.ActivePlayer.Hand.Count + world.TurnManager.ActivePlayer.Deck.Count, "Total cards in deck and hand must sum to 10.");
        }

        [TestMethod]
        public void Build_FallsBackToTestMap_WhenFilesAreMissing()
        {
            var mockDb = new MockWorldBuilderDB();

            // We pass a garbage map path to force the fallback
            var builder = new WorldBuilder(mockDb, "invalid_map.json");

            var world = builder.Build();

            // The Test Map (hardcoded in MapFactory) has exactly 3 nodes.
            Assert.HasCount(3, world.MapManager.Nodes, "Should load the default Test Map (3 nodes) on file error.");
        }
    }
}