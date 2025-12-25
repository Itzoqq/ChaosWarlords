using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class MapRuleEngineTests
    {
        private MapRuleEngine _engine = null!;
        private Player _player1 = null!;
        private Player _player2 = null!;
        private MapNode _node1 = null!, _node2 = null!, _node3 = null!;
        private Site _siteA = null!;

        [TestInitialize]
        public void Setup()
        {
            _player1 = new Player(PlayerColor.Red);
            _player2 = new Player(PlayerColor.Blue);

            // Setup: [Node1] -- [Node2] -- [Node3 (SiteA)]
            _node1 = new MapNode(1, Vector2.Zero);
            _node2 = new MapNode(2, Vector2.Zero);
            _node3 = new MapNode(3, Vector2.Zero);

            _node1.AddNeighbor(_node2);
            _node2.AddNeighbor(_node1);
            _node2.AddNeighbor(_node3);
            _node3.AddNeighbor(_node2);

            _siteA = new NonCitySite("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            _siteA.AddNode(_node3);

            // Manual dependency injection for the engine
            var nodes = new List<MapNode> { _node1, _node2, _node3 };
            var sites = new List<Site> { _siteA };
            var lookup = new Dictionary<MapNode, Site> { { _node3, _siteA } };

            _engine = new MapRuleEngine(nodes, sites, lookup);
        }

        [TestMethod]
        public void HasPresence_True_IfDirectlyOccupying()
        {
            _node1.Occupant = _player1.Color;
            Assert.IsTrue(_engine.HasPresence(_node1, _player1.Color));
        }

        [TestMethod]
        public void HasPresence_True_IfAdjacentToFriendly()
        {
            _node1.Occupant = _player1.Color;
            Assert.IsTrue(_engine.HasPresence(_node2, _player1.Color));
        }

        [TestMethod]
        public void HasPresence_True_IfSiteHasFriendlySpy()
        {
            _siteA.Spies.Add(_player1.Color);
            Assert.IsTrue(_engine.HasPresence(_node3, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_True_IfEmptyAndHasPresence()
        {
            _node1.Occupant = _player1.Color; // Presence source
            Assert.IsTrue(_engine.CanDeployAt(_node2, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_False_IfOccupied()
        {
            _node2.Occupant = PlayerColor.Blue;
            Assert.IsFalse(_engine.CanDeployAt(_node2, _player1.Color));
        }

        [TestMethod]
        public void CanAssassinate_True_IfEnemyAndHasPresence()
        {
            _node1.Occupant = _player1.Color; // Presence
            _node2.Occupant = _player2.Color; // Target
            Assert.IsTrue(_engine.CanAssassinate(_node2, _player1));
        }

        [TestMethod]
        public void CanDeployAt_True_StartOfGame_NoTroopsOnBoard()
        {
            // Rule: If you have NO troops on the map, you can deploy anywhere (Start of game)
            // Setup: Ensure all nodes are empty
            _node1.Occupant = PlayerColor.None;
            _node2.Occupant = PlayerColor.None;
            _node3.Occupant = PlayerColor.None;

            // Assert
            Assert.IsTrue(_engine.CanDeployAt(_node1, _player1.Color));
        }

        [TestMethod]
        public void CanAssassinate_Fails_OnOwnTroop()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player1.Color; // Target is own troop
            Assert.IsFalse(_engine.CanAssassinate(_node2, _player1));
        }

        [TestMethod]
        public void CanMoveSource_True_ForEnemyWithPresence()
        {
            // P1 has presence at Node 1 (via Node 2), Node 1 has Enemy
            _node2.Occupant = _player1.Color;
            _node1.Occupant = _player2.Color;
            Assert.IsTrue(_engine.CanMoveSource(_node1, _player1));
        }

        [TestMethod]
        public void CanMoveSource_True_ForNeutralTroop()
        {
            _node2.Occupant = _player1.Color;
            _node1.Occupant = PlayerColor.Neutral;
            Assert.IsTrue(_engine.CanMoveSource(_node1, _player1));
        }

        [TestMethod]
        public void CanMoveSource_False_ForOwnTroop()
        {
            // Cannot "Move Enemy" on yourself
            _node1.Occupant = _player1.Color;
            Assert.IsFalse(_engine.CanMoveSource(_node1, _player1));
        }

        [TestMethod]
        public void CanMoveSource_False_ForEnemyWithoutPresence()
        {
            // Enemy is at Node 3 (Site A), Player 1 is far away at Node 1 (blocked by Node 2)
            _node3.Occupant = _player2.Color;
            _node1.Occupant = _player1.Color;
            // Node 2 is empty, so no adjacency to Node 3

            Assert.IsFalse(_engine.CanMoveSource(_node3, _player1));
        }

        [TestMethod]
        public void CanMoveDestination_True_ForEmptyNode()
        {
            _node1.Occupant = PlayerColor.None;
            Assert.IsTrue(_engine.CanMoveDestination(_node1));
        }

        [TestMethod]
        public void CanMoveDestination_False_ForOccupiedNode()
        {
            _node1.Occupant = _player2.Color;
            Assert.IsFalse(_engine.CanMoveDestination(_node1));
        }
        [TestMethod]
        public void HasPresence_False_ForAdjacentToSpy()
        {
            // Spy at Site A (Node 3)
            _siteA.Spies.Add(_player1.Color);

            // Check presence at Node 3 (At Site) -> Should be True (Already covered by HasPresence_True_IfSiteHasFriendlySpy)
            // Check presence at Node 2 (Adjacent) -> Should be False
            // Spy logic: Spies grant presence at their specific location, but NEVER adjacency presence.
            
            Assert.IsFalse(_engine.HasPresence(_node2, _player1.Color), "Spy should NOT grant presence to adjacent nodes.");
        }
    }
}