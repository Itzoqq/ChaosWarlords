using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class ActionSystemTests
    {
        private Player _player1 = null!;
        private Player _player2 = null!;
        private IMapManager _mapManager = null!; // Mocked dependency
        private ActionSystem _actionSystem = null!; // System Under Test

        private MapNode _node1 = null!, _node2 = null!;
        private Site _siteA = null!;

        // Helper to capture events
        private bool _eventCompletedFired;
        private bool _eventFailedFired;

        [TestInitialize]
        public void Setup()
        {
            // ARRANGE
            _player1 = new Player(PlayerColor.Red);
            _player2 = new Player(PlayerColor.Blue);

            // Mock the MapManager
            _mapManager = Substitute.For<IMapManager>();

            // Setup Data Entities (Concrete is fine for data holders)
            _node1 = new MapNode(1, new Vector2(10, 10));
            _node2 = new MapNode(2, new Vector2(20, 10));
            _siteA = new Site("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);

            // Inject the mock
            _actionSystem = new ActionSystem(_player1, _mapManager);

            // Subscribe to events for every test
            _eventCompletedFired = false;
            _eventFailedFired = false;
            _actionSystem.OnActionCompleted += (s, e) => _eventCompletedFired = true;
            _actionSystem.OnActionFailed += (s, msg) => _eventFailedFired = true;

            // Reset player defaults
            _player1.Power = 10;
            _player1.TroopsInBarracks = 10;
            _player1.SpiesInBarracks = 5;
        }

        #region 1. Initiation Tests

        [TestMethod]
        public void TryStartAssassinate_Succeeds_WhenPlayerHasEnoughPower()
        {
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
            Assert.IsFalse(_eventFailedFired);
        }

        [TestMethod]
        public void TryStartAssassinate_Fails_WhenPlayerHasNotEnoughPower()
        {
            _player1.Power = 2;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
            Assert.IsTrue(_eventFailedFired, "Should fire failure event due to low power.");
        }

        [TestMethod]
        public void TryStartReturnSpy_Succeeds_WhenPlayerHasEnoughPower()
        {
            _player1.Power = 3;
            _actionSystem.TryStartReturnSpy();
            Assert.AreEqual(ActionState.TargetingReturnSpy, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void TryStartReturnSpy_Fails_WhenPlayerHasNotEnoughPower()
        {
            _player1.Power = 2;
            _actionSystem.TryStartReturnSpy();
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
            Assert.IsTrue(_eventFailedFired);
        }

        #endregion

        #region 2. Basic Execution Tests

        [TestMethod]
        public void HandleTargetClick_Assassinate_PaysCost_AndCallsMapManager()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(0, _player1.Power); // Cost paid
            _mapManager.Received(1).Assassinate(_node2, _player1);
        }

        [TestMethod]
        public void HandleTargetClick_Assassinate_InvalidTarget_Fails()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate);
            _mapManager.CanAssassinate(_node2, _player1).Returns(false);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventFailedFired);
            _mapManager.DidNotReceive().Assassinate(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_PlaceSpy_Succeeds()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy);
            _player1.SpiesInBarracks = 1;

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            _mapManager.Received(1).PlaceSpy(_siteA, _player1);
        }

        [TestMethod]
        public void HandleTargetClick_Supplant_CallsSupplant()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingSupplant);
            _player1.TroopsInBarracks = 1;
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            _mapManager.Received(1).Supplant(_node2, _player1);
        }

        [TestMethod]
        public void HandleTargetClick_Return_CallsReturnTroop()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingReturn);
            _node1.Occupant = _player1.Color;
            _mapManager.HasPresence(_node1, _player1.Color).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            _mapManager.Received(1).ReturnTroop(_node1, _player1);
        }

        #endregion

        #region 3. Spy Return Logic (Complex)

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_AutoResolves_SingleFaction()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Mock: Only Blue spies here
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue });
            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(0, _player1.Power);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue);
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_DetectsAmbiguity_MultipleFactions()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Mock: Blue AND Neutral spies here
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(_eventCompletedFired, "Should wait for selection.");
            Assert.AreEqual(ActionState.SelectingSpyToReturn, _actionSystem.CurrentState);
            Assert.AreEqual(_siteA, _actionSystem.PendingSite);

            _mapManager.DidNotReceive().ReturnSpecificSpy(Arg.Any<Site>(), Arg.Any<Player>(), Arg.Any<PlayerColor>());
        }

        [TestMethod]
        public void FinalizeSpyReturn_CompletesAction_AndPaysCost()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Set up ambiguity to set PendingSite
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });
            _actionSystem.HandleTargetClick(null, _siteA);

            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Neutral).Returns(true);

            // Act
            _actionSystem.FinalizeSpyReturn(PlayerColor.Neutral);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(0, _player1.Power);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Neutral);
        }

        #endregion

        #region 4. Edge Cases & Failures (The "Omitted" Tests)

        [TestMethod]
        public void HandleTargetClick_Assassinate_Fails_IfPowerLostDuringTargeting()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            _player1.Power = 0; // Lost power while targeting (e.g. interruption)

            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            Assert.IsTrue(_eventFailedFired);
            _mapManager.DidNotReceive().Assassinate(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_Fails_IfPowerLostDuringTargeting()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.TryStartReturnSpy();
            _player1.Power = 2; // Lost power below cost (3)

            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue });

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            Assert.IsTrue(_eventFailedFired);
            _mapManager.DidNotReceive().ReturnSpecificSpy(Arg.Any<Site>(), Arg.Any<Player>(), Arg.Any<PlayerColor>());
        }

        [TestMethod]
        public void HandleTargetClick_PlaceSpy_Fails_IfSpyAlreadyThere()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy);
            _siteA.Spies.Add(_player1.Color); // Already have a spy here

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            _mapManager.DidNotReceive().PlaceSpy(Arg.Any<Site>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_PlaceSpy_Fails_IfNoSpiesInBarracks()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy);
            _player1.SpiesInBarracks = 0;

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            _mapManager.DidNotReceive().PlaceSpy(Arg.Any<Site>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_Supplant_Fails_IfNoTroopsInBarracks()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingSupplant);
            _player1.TroopsInBarracks = 0;
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            _mapManager.DidNotReceive().Supplant(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_Return_Fails_IfTroopNeutral()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingReturn);
            _node1.Occupant = PlayerColor.Neutral; // Cannot return white troops
            _mapManager.HasPresence(_node1, _player1.Color).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            _mapManager.DidNotReceive().ReturnTroop(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        #endregion
    }
}