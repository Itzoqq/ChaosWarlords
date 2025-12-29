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
            _testSite = TestData.Sites.NeutralSite();
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
            var node = TestData.MapNodes.Node1();
            var player = TestData.Players.RedPlayer();
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
            var node = TestData.MapNodes.Node1();
            var player = TestData.Players.PoorPlayer();
            // TestData.Players.PoorPlayer() has Red color and 0 resources.
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
            var node = TestData.MapNodes.Node1();
            node.Occupant = PlayerColor.Blue;
            var attacker = TestData.Players.RedPlayer();
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
            var source = TestData.MapNodes.Node1();
            source.Occupant = PlayerColor.Red;
            var destination = TestData.MapNodes.Node2();
            destination.Occupant = PlayerColor.None;
            var player = TestData.Players.RedPlayer();

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
            var node = TestData.MapNodes.Node1();
            node.Occupant = PlayerColor.Blue;
            var attacker = TestData.Players.RedPlayer();
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
            var node = TestData.MapNodes.Node1();
            node.Occupant = PlayerColor.Red;
            var player = TestData.Players.RedPlayer();
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



