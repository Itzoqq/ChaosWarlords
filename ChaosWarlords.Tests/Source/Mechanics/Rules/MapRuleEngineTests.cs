using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;

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
            // Default to Playing for legacy tests
            _engine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Playing);
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

        // -------------------------------------------------------------------------
        // MATCH PHASE TESTS
        // -------------------------------------------------------------------------

        [TestMethod]
        public void CanDeployAt_SetupPhase_True_ForStartingSite()
        {
            // Arrange: Setup Phase needs a StartingSite
            var startNode = new MapNode(99, Vector2.Zero);
            var startSite = new StartingSite("Start", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            startSite.AddNode(startNode);
            
            // Re-init engine with this new site included
            var nodes = new List<MapNode> { startNode };
            var sites = new List<Site> { startSite };
            var lookup = new Dictionary<MapNode, Site> { { startNode, startSite } };
            var localEngine = new MapRuleEngine(nodes, sites, lookup);
            
            localEngine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Setup);

            // Act & Assert
            // Player has 0 troops, Target is StartingSite -> Should be TRUE
            Assert.IsTrue(localEngine.CanDeployAt(startNode, _player1.Color));
        }

        [TestMethod]
        public void CanDeployAt_SetupPhase_False_ForNormalSite()
        {
            _engine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Setup);
            
            // _siteA is NonCitySite (Normal)
            Assert.IsFalse(_engine.CanDeployAt(_node3, _player1.Color), "Should not allow deployment on Normal Site in Setup Phase.");
        }

        [TestMethod]
        public void CanDeployAt_SetupPhase_False_IfHasTroops()
        {
            var startNode = new MapNode(99, Vector2.Zero);
            var startSite = new StartingSite("Start", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            startSite.AddNode(startNode);

            // Re-init engine 
            var nodes = new List<MapNode> { startNode, _node1 };
            var sites = new List<Site> { startSite };
            var lookup = new Dictionary<MapNode, Site> { { startNode, startSite } };
            var localEngine = new MapRuleEngine(nodes, sites, lookup);
            localEngine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Setup);

            // Player already has a troop somewhere else
            _node1.Occupant = _player1.Color; 

            // Act & Assert
            // Player has >0 troops -> Should be FALSE
            Assert.IsFalse(localEngine.CanDeployAt(startNode, _player1.Color), "Should only allow 1 troop placement in Setup Phase.");
        }

        [TestMethod]
        public void CanDeployAt_PlayingPhase_StandardRules()
        {
            _engine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Playing);

            // 0 Troops -> Can deploy anywhere (Standard Rule)
            Assert.IsTrue(_engine.CanDeployAt(_node1, _player1.Color));

            // Has Troops -> Must have presence
            _node1.Occupant = _player1.Color;
            Assert.IsTrue(_engine.CanDeployAt(_node2, _player1.Color)); // Adjacent
            Assert.IsFalse(_engine.CanDeployAt(_node3, _player1.Color)); // Not adjacent (Node 3 connects to 2, not 1 directly, wait. 1->2->3. 1 is adjacent to 2. 2 is adjacent to 3. 1 is NOT adjacent to 3.)
        }

        [TestMethod]
        public void CanDeployAt_SetupPhase_False_IfStartingSiteOccupiedByOtherPlayer()
        {
            // Arrange: Create a StartingSite with 2 nodes
            var startNode1 = new MapNode(100, Vector2.Zero);
            var startNode2 = new MapNode(101, new Vector2(50, 0));
            var startSite = new StartingSite("Start", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            startSite.AddNode(startNode1);
            startSite.AddNode(startNode2);
            
            var nodes = new List<MapNode> { startNode1, startNode2 };
            var sites = new List<Site> { startSite };
            var lookup = new Dictionary<MapNode, Site> { { startNode1, startSite }, { startNode2, startSite } };
            var localEngine = new MapRuleEngine(nodes, sites, lookup);
            localEngine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Setup);

            // Player 1 occupies one node of the Starting Site
            startNode1.Occupant = _player1.Color;

            // Act & Assert
            // Player 2 should NOT be able to deploy to the same Starting Site
            Assert.IsFalse(localEngine.CanDeployAt(startNode2, _player2.Color), 
                "Should not allow multiple players in the same Starting Site during Setup Phase.");
        }

        [TestMethod]
        public void CanDeployAt_PlayingPhase_True_IfWipedFromBoard()
        {
            // Arrange
            _engine.SetPhase(ChaosWarlords.Source.Contexts.MatchPhase.Playing);
            
            // Ensure Player 1 is wiped (no troops on map)
            _node1.Occupant = PlayerColor.None;
            _node2.Occupant = PlayerColor.None;
            _node3.Occupant = PlayerColor.None;

            // Act & Assert
            // Should be able to deploy ANYWHERE (e.g., node 3 which is far away and disconnected)
            Assert.IsTrue(_engine.CanDeployAt(_node3, _player1.Color), "Wiped player should be able to deploy anywhere.");
        }
    }
}


