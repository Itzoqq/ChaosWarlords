using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class ActionSystemTests
    {
        private Player _player1 = null!;
        private Player _player2 = null!;
        private MapManager _mapManager = null!;
        private ActionSystem _actionSystem = null!;

        private MapNode _node1 = null!, _node2 = null!;
        private Site _siteA = null!;

        [TestInitialize]
        public void Setup()
        {
            // ARRANGE
            _player1 = new Player(PlayerColor.Red);
            _player2 = new Player(PlayerColor.Blue);

            _node1 = new MapNode(1, new(10, 10), null);
            _node2 = new MapNode(2, new(20, 10), null);
            _node1.AddNeighbor(_node2);

            _siteA = new Site("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            _siteA.AddNode(_node2);

            var nodes = new List<MapNode> { _node1, _node2 };
            var sites = new List<Site> { _siteA };
            _mapManager = new MapManager(nodes, sites);

            _actionSystem = new ActionSystem(_player1, _mapManager);

            // Reset player
            _player1.Power = 10;
            _player1.TroopsInBarracks = 10;
        }

        #region Action Initiation Tests

        [TestMethod]
        public void TryStartAssassinate_Succeeds_WhenPlayerHasEnoughPower()
        {
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(GameState.TargetingAssassinate, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void TryStartAssassinate_Fails_WhenPlayerHasNotEnoughPower()
        {
            _player1.Power = 2;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(GameState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void TryStartReturnSpy_Succeeds_WhenPlayerHasEnoughPower()
        {
            _player1.Power = 3;
            _actionSystem.TryStartReturnSpy();
            Assert.AreEqual(GameState.TargetingReturnSpy, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void TryStartReturnSpy_Fails_WhenPlayerHasNotEnoughPower()
        {
            _player1.Power = 2;
            _actionSystem.TryStartReturnSpy();
            Assert.AreEqual(GameState.Normal, _actionSystem.CurrentState);
        }

        #endregion

        #region Action Handling Tests

        [TestMethod]
        public void HandleTargetClick_Assassinate_PaysCostForUIAction()
        {
            // Arrange: Start a UI-based assassination (no card)
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(GameState.TargetingAssassinate, _actionSystem.CurrentState);

            // Set up a valid target
            _node1.Occupant = _player1.Color; // Presence
            _node2.Occupant = _player2.Color; // Target

            // Act: Click the target
            bool success = _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(0, _player1.Power); // Cost was paid
            Assert.AreEqual(PlayerColor.None, _node2.Occupant); // Target is gone
        }

        [TestMethod]
        public void HandleTargetClick_Assassinate_DoesNotPayCostForCardAction()
        {
            // Arrange: Start a card-based assassination
            var card = new Card("Assassin", "Assassin Name", 0, CardAspect.Shadow, 0, 0);
            _player1.Power = 3; // Has power, but shouldn't be used
            _actionSystem.StartTargeting(GameState.TargetingAssassinate, card);

            // Set up a valid target
            _node1.Occupant = _player1.Color; // Presence
            _node2.Occupant = _player2.Color; // Target

            // Act: Click the target
            bool success = _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(3, _player1.Power); // Cost was NOT paid by ActionSystem
            Assert.AreEqual(PlayerColor.None, _node2.Occupant);
        }

        [TestMethod]
        public void HandleTargetClick_PlaceSpy_SucceedsOnSiteAndFailsOnNode()
        {
            // Arrange: Start a Place Spy action
            _actionSystem.StartTargeting(GameState.TargetingPlaceSpy, null);
            _player1.SpiesInBarracks = 1;

            // Act 1: Click a valid site
            bool successOnSite = _actionSystem.HandleTargetClick(null, _siteA);

            // Assert 1
            Assert.IsTrue(successOnSite);
            Assert.HasCount(1, _siteA.Spies);
            Assert.AreEqual(0, _player1.SpiesInBarracks);

            // Arrange 2: Reset and try to click a node
            _siteA.Spies.Clear();
            _player1.SpiesInBarracks = 1;
            _actionSystem.StartTargeting(GameState.TargetingPlaceSpy, null);

            // Act 2: Click a node (invalid target for this action)
            bool successOnNode = _actionSystem.HandleTargetClick(_node1, null);

            // Assert 2
            Assert.IsFalse(successOnNode);
            Assert.IsEmpty(_siteA.Spies);
        }

        [TestMethod]
        public void HandleTargetClick_Fails_WithInvalidTarget()
        {
            // Arrange: Try to assassinate a friendly troop
            _actionSystem.StartTargeting(GameState.TargetingAssassinate, null);
            _node1.Occupant = _player1.Color; // Friendly troop

            // Act
            bool success = _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual(_player1.Color, _node1.Occupant); // Nothing should have changed
        }
        #endregion
    }
}
