using NSubstitute;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// Integration tests for Return Unit mechanics.
    /// Tests ensure troops are correctly returned to barracks for both friendly and enemy troops.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ReturnUnitMechanicsTests
    {
        private Player _player1 = null!;
        private Player _player2 = null!;
        private MapManager _mapManager = null!;
        private ITurnManager _turnManager = null!;
        private IPlayerStateManager _stateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            Utilities.TestLogger.Initialize();
            var logger = Utilities.TestLogger.Instance;

            _player1 = TestData.Players.RedPlayer();
            _player1.TroopsInBarracks = 10;

            _player2 = TestData.Players.BluePlayer();
            _player2.TroopsInBarracks = 10;

            _stateManager = new PlayerStateManager(logger);

            // Create mock TurnManager with GetPlayerByColor
            _turnManager = Substitute.For<ITurnManager>();
            _turnManager.GetPlayerByColor(PlayerColor.Red).Returns(_player1);
            _turnManager.GetPlayerByColor(PlayerColor.Blue).Returns(_player2);

            var node1 = new MapNode(1, new Microsoft.Xna.Framework.Vector2(0, 0));
            var node2 = new MapNode(2, new Microsoft.Xna.Framework.Vector2(50, 0));
            node1.AddNeighbor(node2);
            node2.AddNeighbor(node1);

            var nodes = new List<MapNode> { node1, node2 };
            var sites = new List<Site>();

            _mapManager = new MapManager(nodes, sites, _turnManager, logger, _stateManager);
            _mapManager.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Playing);
        }

        [TestMethod]
        public void ReturnTroop_ReturnsFriendlyTroopToOwnBarracks()
        {
            // Arrange
            var node = _mapManager.NodesInternal[0];
            node.Occupant = _player1.Color;
            int initialTroops = _player1.TroopsInBarracks;

            // Act
            _mapManager.ReturnTroop(node, _player1);

            // Assert
            Assert.AreEqual(PlayerColor.None, node.Occupant, "Node should be empty");
            Assert.AreEqual(initialTroops + 1, _player1.TroopsInBarracks, "Player 1 should have 1 more troop in barracks");
        }

        [TestMethod]
        public void ReturnTroop_ReturnsEnemyTroopToEnemyBarracks()
        {
            // Arrange
            var node1 = _mapManager.NodesInternal[0];
            var node2 = _mapManager.NodesInternal[1];

            // Player 1 has presence at node1 (needed to perform Return action)
            node1.Occupant = _player1.Color;

            // Player 2's troop is at node2
            node2.Occupant = _player2.Color;

            int player1InitialTroops = _player1.TroopsInBarracks;
            int player2InitialTroops = _player2.TroopsInBarracks;

            // Act: Player 1 returns Player 2's troop
            _mapManager.ReturnTroop(node2, _player1);

            // Assert
            Assert.AreEqual(PlayerColor.None, node2.Occupant, "Node should be empty");
            Assert.AreEqual(player1InitialTroops, _player1.TroopsInBarracks, "Player 1's barracks should not change");
            Assert.AreEqual(player2InitialTroops + 1, _player2.TroopsInBarracks, "Player 2 should have 1 more troop in barracks (BUG FIX)");
        }

        [TestMethod]
        public void ReturnTroop_HandlesNullPlayerGracefully()
        {
            // Arrange
            var node1 = _mapManager.NodesInternal[0];
            var node2 = _mapManager.NodesInternal[1];

            // Player 1 needs presence to perform return action
            node1.Occupant = _player1.Color;

            // Node 2 has a troop from a color with no player
            node2.Occupant = PlayerColor.Black;

            // Mock returns null for Black
            _turnManager.GetPlayerByColor(PlayerColor.Black).Returns((Player?)null);

            // Act & Assert - should not throw
            _mapManager.ReturnTroop(node2, _player1);

            // Node should still be cleared even if player not found
            Assert.AreEqual(PlayerColor.None, node2.Occupant, "Node should be empty even if player not found");
        }
    }
}
