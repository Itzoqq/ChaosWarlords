using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using NSubstitute; // Assuming NSubstitute is available or using manual mocks if not.
// Analysis of codebase shows manual DI is used, but let's see if we can use the real classes where possible.
// MapRuleEngineTests uses real classes. ActionSystem uses interfaces.
// I will use Manual Mocks or Real Implementations.

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class GameLogicTests
    {
        // Integration-style tests for Logic
        private MapRuleEngine _ruleEngine;
        private ActionSystem _actionSystem;
        private MapManager _mapManager;
        private TurnManager _turnManager;
        private Player _player1;
        private Player _player2;

        private MapNode _node1, _node2;
        private Site _siteA;

        [TestInitialize]
        public void Setup()
        {
            _player1 = new Player(PlayerColor.Red) { Power = 10 };
            _player2 = new Player(PlayerColor.Blue);
            var players = new List<Player> { _player1, _player2 };

            // Setup: Site A (Node 1) -- Node 2
            _node1 = new MapNode(1, Vector2.Zero);
            _node2 = new MapNode(2, Vector2.Zero);
            _node1.AddNeighbor(_node2);
            _node2.AddNeighbor(_node1);

            _siteA = new Site("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            _siteA.AddNode(_node1);

            var nodes = new List<MapNode> { _node1, _node2 };
            var sites = new List<Site> { _siteA };
            var lookup = new Dictionary<MapNode, Site> { { _node1, _siteA } };

            _ruleEngine = new MapRuleEngine(nodes, sites, lookup);
            _mapManager = new MapManager(nodes, sites); // This uses its own rule engine internally
            
            _turnManager = new TurnManager(players);
            _actionSystem = new ActionSystem(_turnManager, _mapManager);
        }

        [TestMethod]
        public void Rule_SpyDoesNotGrantAdjacencyPresence()
        {
            // Spy at SiteA (Node1)
            _siteA.Spies.Add(_player1.Color);

            // Check presence at Node 1 (At Site) -> Should be True
            bool presenceAtSite = _mapManager.HasPresence(_node1, _player1.Color);
            Assert.IsTrue(presenceAtSite, "Spy should grant presence at its own site.");

            // Check presence at Node 2 (Adjacent) -> Should be False
            bool presenceAdjacent = _mapManager.HasPresence(_node2, _player1.Color);
            Assert.IsFalse(presenceAdjacent, "Spy should NOT grant presence to adjacent nodes.");
        }

        [TestMethod]
        public void Action_ReturnSpy_FailsWithoutPresence()
        {
            // Enemy Spy at Site A
            _siteA.Spies.Add(_player2.Color);
            
            // Player 1 has NO presence at Site A (Empty nodes, no spies)
            Assert.IsFalse(_mapManager.HasPresence(_node1, _player1.Color));

            // Attempt to Return Spy via Public API
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);
            
            // We expect this to FAIL (ActionFailed event) or simply not proceed.
            bool failed = false;
            _actionSystem.OnActionFailed += (s, e) => failed = true;

            _actionSystem.HandleTargetClick(null, _siteA); // Public Entry Point

            // The logic: 
            // 1. IsValidSpyReturnTarget checks presence? No, it checks args.
            // 2. ExecuteReturnSpy -> Finalize (if 1 spy) -> MapManager.ReturnSpecificSpy -> Checks Presence.
            // 3. Should fail.
            
            Assert.IsTrue(failed, "Action should have fired OnActionFailed due to lack of presence.");
        }

        [TestMethod]
        public void Action_ReturnSpy_DoesNotSpendPower_OnFailure()
        {
            // Enemy Spy at Site A
            _siteA.Spies.Add(_player2.Color);
            int initialPower = _player1.Power;

            // Setup
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);
            
            // Hook event
            bool failed = false;
            _actionSystem.OnActionFailed += (s, e) => failed = true;

            // Interact
            _actionSystem.HandleTargetClick(null, _siteA);
            
            // Check Result
            Assert.IsTrue(failed, "Action must fail.");
            Assert.AreEqual(initialPower, _player1.Power, "Power should NOT be spent if action failed.");
            Assert.IsTrue(_siteA.Spies.Contains(_player2.Color), "Spy should still be there.");
        }

        [TestMethod]
        public void Logic_TotalControl_DeniedByEnemySpy()
        {
             // P1 Controls Node 1
             _node1.Occupant = _player1.Color;
             
             // Initial Check
             _mapManager.RecalculateSiteState(_siteA, _player1);
             Assert.IsTrue(_siteA.HasTotalControl, "Should have total control initially.");

             // Enemy Spy Arrives
             _siteA.Spies.Add(_player2.Color);
             _mapManager.RecalculateSiteState(_siteA, _player1);

             Assert.IsFalse(_siteA.HasTotalControl, "Enemy spy should deny Total Control.");
             Assert.AreEqual(_player1.Color, _siteA.Owner, "Should still OWN the site (Minority Control).");
        }
    }
}

// Helper to access internal methods/properties if needed, 
// though we should stick to public API as much as possible.
// For ActionSystem, we might need to cast or rely on public flows.
