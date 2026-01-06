using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using NSubstitute;
using ChaosWarlords.Source.Utilities;

using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;
using ChaosWarlords.Source.Entities.Actors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Integration.Factories
{
    [TestClass]

    [TestCategory("Integration")]
    public class MatchFactoryTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            var builder = new MatchFactory(mockDb, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var replayManager = Substitute.For<IReplayManager>();
            var result = builder.Build(replayManager);

            Assert.IsNotNull(result.TurnManager.ActivePlayer);
            Assert.IsNotNull(result.MapManager);
        }

        [TestMethod]
        public void Verify_SeatIndex_IsStable_Deterministic()
        {
             var mockDb = Substitute.For<ICardDatabase>();
             mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
             mockDb.GetAllMarketCards(null).Returns(new List<Card>()); // Handle optional argument
             
             var factory = new MatchFactory(mockDb, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
             var replayManagerMock = Substitute.For<IReplayManager>();
             
             // Run 1
             var world1 = factory.Build(replayManagerMock, 555);
             var p1_red = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
             var p1_blue = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
             
             // Run 2
             var world2 = factory.Build(replayManagerMock, 555);
             var p2_red = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
             var p2_blue = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
             
             Assert.AreEqual(p1_red.SeatIndex, p2_red.SeatIndex);
             Assert.AreEqual(p1_blue.SeatIndex, p2_blue.SeatIndex);
             
             Assert.AreNotEqual(p1_red.SeatIndex, p1_blue.SeatIndex);
        }
        [TestMethod]
        public void ApplyScenarioRules_AddsSpies_ToCityOfGold()
        {
            // Arrange
            var nodes = new List<MapNode>();
            var sites = new List<Site>();
            // Use concrete CitySite
            var cityOfGold = new CitySite("The City of Gold", ResourceType.Power, 1, ResourceType.Influence, 1);
            cityOfGold.Id = 1;
            sites.Add(cityOfGold);

            var mapManager = new MapManager(nodes, sites, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            CollectionAssert.Contains(cityOfGold.Spies, PlayerColor.Blue);
            CollectionAssert.Contains(cityOfGold.Spies, PlayerColor.Red);
            CollectionAssert.Contains(cityOfGold.Spies, PlayerColor.Neutral);
        }

        [TestMethod]
        public void ApplyScenarioRules_DoesNothing_ForNormalSites()
        {
            // Arrange
            var nodes = new List<MapNode>();
            var sites = new List<Site>();
            var normalSite = new CitySite("Normal Forest", ResourceType.Power, 1, ResourceType.Influence, 1);
            normalSite.Id = 2;
            sites.Add(normalSite);

            var mapManager = new MapManager(nodes, sites, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            CollectionAssert.AreEqual(new List<PlayerColor>(), normalSite.Spies);
        }

        [TestMethod]
        public void ApplyScenarioRules_HandlesNullSites_Gracefully()
        {
            // Arrange
            // We need a map manager with null sites. 
            // MapManager constructor assigns directly.
            var mapManager = new MapManager(new List<MapNode>(), null!, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            // Should not throw exception
        }
    }
}



