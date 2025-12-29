using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Managers;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]

    [TestCategory("Unit")]
    public class CombatResolverTests
    {
        private CombatResolver _resolver = null!;
        private Site _testSite = null!;
        private bool _siteRecalculated = false;
        private MatchPhase _currentPhase = MatchPhase.Playing;

        [TestInitialize]
        public void Setup()
        {
            _testSite = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            _siteRecalculated = false;

            var stateManager = new PlayerStateManager();

            _resolver = new CombatResolver(
                node => _testSite,
                (site, player) => _siteRecalculated = true,
                () => _currentPhase,
                stateManager
            );
        }

        [TestMethod]
        public void ExecuteDeploy_DeploysTroopAndDecreasesBarracks()
        {
            // Arrange
            var node = new MapNodeBuilder().WithId(1).Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            player.TroopsInBarracks = 5;
            player.Power = 10;

            // Act
            _resolver.ExecuteDeploy(node, player);

            // Assert
            Assert.AreEqual(PlayerColor.Red, node.Occupant);
            Assert.AreEqual(4, player.TroopsInBarracks);
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteDeploy_InSetupPhase_DoesNotConsumePower()
        {
            // Arrange
            _currentPhase = MatchPhase.Setup;
            var node = new MapNodeBuilder().WithId(1).Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            player.TroopsInBarracks = 5;
            player.Power = 0; // No power

            // Act
            _resolver.ExecuteDeploy(node, player);

            // Assert
            Assert.AreEqual(PlayerColor.Red, node.Occupant);
            Assert.AreEqual(0, player.Power); // Power unchanged
        }

        [TestMethod]
        public void ExecuteAssassinate_RemovesTroopAndIncreasesTophyHall()
        {
            // Arrange
            var node = new MapNodeBuilder().WithId(1).Build();
            node.Occupant = PlayerColor.Blue;
            var attacker = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            attacker.TrophyHall = 0;

            // Act
            _resolver.ExecuteAssassinate(node, attacker);

            // Assert
            Assert.AreEqual(PlayerColor.None, node.Occupant);
            Assert.AreEqual(1, attacker.TrophyHall);
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteMove_MovesTroopBetweenNodes()
        {
            // Arrange
            var source = new MapNodeBuilder().WithId(1).Build();
            source.Occupant = PlayerColor.Red;
            var destination = new MapNodeBuilder().WithId(2).Build();
            destination.Occupant = PlayerColor.None;
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();

            // Act
            _resolver.ExecuteMove(source, destination, player);

            // Assert
            Assert.AreEqual(PlayerColor.None, source.Occupant);
            Assert.AreEqual(PlayerColor.Red, destination.Occupant);
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteSupplant_AssassinatesAndDeploys()
        {
            // Arrange
            var node = new MapNodeBuilder().WithId(1).Build();
            node.Occupant = PlayerColor.Blue;
            var attacker = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            attacker.TroopsInBarracks = 5;
            attacker.Power = 10;
            attacker.TrophyHall = 0;

            // Act
            _resolver.ExecuteSupplant(node, attacker);

            // Assert
            Assert.AreEqual(PlayerColor.Red, node.Occupant); // Deployed
            Assert.AreEqual(1, attacker.TrophyHall); // Assassinated
            Assert.AreEqual(4, attacker.TroopsInBarracks); // Deployed
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteReturnTroop_ReturnsFriendlyTroopToBarracks()
        {
            // Arrange
            var node = new MapNodeBuilder().WithId(1).Build();
            node.Occupant = PlayerColor.Red;
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            player.TroopsInBarracks = 3;

            // Act
            _resolver.ExecuteReturnTroop(node, player);

            // Assert
            Assert.AreEqual(PlayerColor.None, node.Occupant);
            Assert.AreEqual(4, player.TroopsInBarracks);
            Assert.IsTrue(_siteRecalculated);
        }
    }
}



