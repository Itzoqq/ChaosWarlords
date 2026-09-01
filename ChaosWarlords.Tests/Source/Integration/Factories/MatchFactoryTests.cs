using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using NSubstitute;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;

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

            var builder = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
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

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
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
        public void Build_WithFourPlayerColors_CreatesAllFourPlayers()
        {
            // Regression test: MatchFactory used to hardcode exactly Red/Blue - no path in the
            // codebase had ever built a 3-4 player match despite PlayerColor supporting 4 seats
            // (rulebook p.4: 2-4 players). See planning.txt.
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black, PlayerColor.Orange };

            var world = factory.Build(replayManagerMock, seed: 555, playerColors: colors);

            Assert.HasCount(4, world.TurnManager.Players);
            for (int i = 0; i < colors.Length; i++)
            {
                var player = world.TurnManager.Players.Single(p => p.Color == colors[i]);
                Assert.AreEqual(i, player.SeatIndex);
            }
        }

        [TestMethod]
        public void Build_WithThreePlayerColors_CreatesAllThreePlayers()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black };

            var world = factory.Build(replayManagerMock, seed: 555, playerColors: colors);

            Assert.HasCount(3, world.TurnManager.Players);
        }

        [TestMethod]
        public void Build_WithoutPlayerColors_DefaultsToRedBlue_UnchangedFromBefore()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());

            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();

            var world = factory.Build(replayManagerMock, seed: 555);

            Assert.HasCount(2, world.TurnManager.Players);
            Assert.IsTrue(world.TurnManager.Players.Any(p => p.Color == PlayerColor.Red));
            Assert.IsTrue(world.TurnManager.Players.Any(p => p.Color == PlayerColor.Blue));
        }

        [TestMethod]
        public void Build_WithOnePlayerColor_ThrowsArgumentException()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();

            Assert.ThrowsExactly<ArgumentException>(() =>
                factory.Build(replayManagerMock, playerColors: new[] { PlayerColor.Red }));
        }

        [TestMethod]
        public void Build_WithFivePlayerColors_ThrowsArgumentException()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            var factory = new MatchFactory(mockDb, Utilities.TestLogger.Instance);
            var replayManagerMock = Substitute.For<IReplayManager>();
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black, PlayerColor.Orange, PlayerColor.Red };

            Assert.ThrowsExactly<ArgumentException>(() =>
                factory.Build(replayManagerMock, playerColors: colors));
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

            var mapManager = new MapManager(nodes, sites, Substitute.For<ITurnManager>(), Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>());

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

            var mapManager = new MapManager(nodes, sites, Substitute.For<ITurnManager>(), Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>());

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
            var mapManager = new MapManager(new List<MapNode>(), null!, Substitute.For<ITurnManager>(), Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>());

            // Act
            MatchFactory.ApplyScenarioRules(mapManager);

            // Assert
            // Should not throw exception
        }
    }
}



