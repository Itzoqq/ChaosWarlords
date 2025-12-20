using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class WorldBuilderTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            // Arrange
            // Create the NSubstitute mock
            var mockDb = Substitute.For<ICardDatabase>();

            // Configure the mock to return an empty list (prevents null reference if used)
            mockDb.GetAllMarketCards().Returns(new List<Card>());

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
            // Arrange
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            var builder = new WorldBuilder(mockDb, "dummy_map.json");

            // Act
            var world = builder.Build();

            // Assert
            // 10 cards total (7 Nobles + 3 Soldiers)
            Assert.HasCount(0, world.TurnManager.ActivePlayer.Hand, "Hand should be empty immediately after WorldBuilder.Build()");
            Assert.HasCount(10, world.TurnManager.ActivePlayer.Deck, "Deck should contain all 10 starting cards.");
            Assert.AreEqual(10, world.TurnManager.ActivePlayer.Hand.Count + world.TurnManager.ActivePlayer.Deck.Count, "Total cards in deck and hand must sum to 10.");
        }

        [TestMethod]
        public void Build_FallsBackToTestMap_WhenFilesAreMissing()
        {
            // Arrange
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            // We pass a garbage map path to force the fallback
            var builder = new WorldBuilder(mockDb, "invalid_map.json");

            // Act
            var world = builder.Build();

            // Assert
            // The Test Map (hardcoded in MapFactory) has exactly 3 nodes.
            Assert.HasCount(3, world.MapManager.NodesInternal, "Should load the default Test Map (3 nodes) on file error.");
        }
    }
}