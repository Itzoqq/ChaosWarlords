using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
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

            _node1 = new MapNode(1, new Vector2(10, 10));
            _node2 = new MapNode(2, new Vector2(20, 10));

            // FIX: Make neighbors bidirectional so HasPresence works correctly
            _node1.AddNeighbor(_node2);
            _node2.AddNeighbor(_node1);

            _siteA = new Site("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            _siteA.AddNode(_node2);

            var nodes = new List<MapNode> { _node1, _node2 };
            var sites = new List<Site> { _siteA };
            _mapManager = new MapManager(nodes, sites);

            _actionSystem = new ActionSystem(_player1, _mapManager);

            // Reset player
            _player1.Power = 10;
            _player1.TroopsInBarracks = 10;

            // Establish Presence for Player 1
            _node1.Occupant = _player1.Color;
        }

        #region Action Initiation Tests

        [TestMethod]
        public void TryStartAssassinate_Succeeds_WhenPlayerHasEnoughPower()
        {
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void TryStartAssassinate_Fails_WhenPlayerHasNotEnoughPower()
        {
            _player1.Power = 2;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
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
        }

        #endregion

        #region Action Handling Tests

        [TestMethod]
        public void HandleTargetClick_Assassinate_PaysCostForUIAction()
        {
            // Arrange: Start a UI-based assassination (no card)
            _player1.Power = 3;
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);

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
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

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
            _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy, null);
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
            _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy, null);

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
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, null);
            _node1.Occupant = _player1.Color; // Friendly troop

            // Act
            bool success = _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual(_player1.Color, _node1.Occupant); // Nothing should have changed
        }

        [TestMethod]
        public void HandleTargetClick_Supplant_SucceedsWithValidTarget()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingSupplant, null);
            _node1.Occupant = _player1.Color; // Presence
            _node2.Occupant = _player2.Color; // Target
            _player1.TroopsInBarracks = 1;
            _player1.TrophyHall = 0;

            // Act
            bool success = _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(_player1.Color, _node2.Occupant); // Node was supplanted
            Assert.AreEqual(1, _player1.TrophyHall);
            Assert.AreEqual(0, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void HandleTargetClick_Return_SucceedsOnOwnTroop()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingReturn, null);
            _node1.Occupant = _player1.Color; // Troop to return. This troop provides its own presence.
            _player1.TroopsInBarracks = 5;

            // Act
            bool success = _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(PlayerColor.None, _node1.Occupant);
            Assert.AreEqual(6, _player1.TroopsInBarracks);
        }

        [TestMethod]
        public void HandleTargetClick_Assassinate_FailsOnEmptyNode()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, null);
            _node1.Occupant = _player1.Color; // Presence
            // _node2 is empty by default

            // Act
            bool success = _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState, "Should remain in targeting state after invalid click.");
        }
        #endregion

        #region Spy Logic Complex Scenarios

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_AutoResolves_SingleFaction()
        {
            // Arrange: 1 Blue Spy
            _siteA.Spies.Add(PlayerColor.Blue);
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Act: Click the site
            bool success = _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsTrue(success, "Should auto-resolve since there is only one enemy faction.");
            Assert.IsEmpty(_siteA.Spies, "Spy should be removed.");
            Assert.AreEqual(0, _player1.Power, "Cost should be paid.");
            // The ActionSystem signals success by returning true; the calling state (e.g., GameplayState) is responsible for resetting the state.
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_AutoResolves_MultipleSpiesSameFaction()
        {
            // Arrange: 2 Blue Spies (Ambiguity only exists if factions differ, not units)
            _siteA.Spies.Add(PlayerColor.Blue);
            _siteA.Spies.Add(PlayerColor.Blue);
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Act
            bool success = _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsTrue(success, "Should auto-resolve because all spies belong to Blue.");
            Assert.HasCount(1, _siteA.Spies, "Only ONE spy should be removed.");
            Assert.AreEqual(0, _player1.Power);
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_DetectsAmbiguity_MultipleFactions()
        {
            // Arrange: 1 Blue Spy, 1 Neutral Spy
            _siteA.Spies.Add(PlayerColor.Blue);
            _siteA.Spies.Add(PlayerColor.Neutral);
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Act
            bool success = _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(success, "Should NOT return true immediately; waiting for selection.");
            Assert.AreEqual(ActionState.SelectingSpyToReturn, _actionSystem.CurrentState);
            Assert.AreEqual(_siteA, _actionSystem.PendingSite);
            Assert.AreEqual(3, _player1.Power, "Power should NOT be deducted yet.");
            Assert.HasCount(2, _siteA.Spies, "Spies should still be there.");
        }

        [TestMethod]
        public void FinalizeSpyReturn_CompletesAction_AndPaysCost()
        {
            // Arrange: We are already in the "Ambiguity" state from previous test
            _siteA.Spies.Add(PlayerColor.Blue);
            _siteA.Spies.Add(PlayerColor.Neutral);
            _player1.Power = 3;

            // To test finalization, we must first get the system into the correct state.
            // 1. Start the 'Return Spy' action.
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);
            // 2. Click the site. This detects ambiguity and transitions the state to 'SelectingSpyToReturn'.
            _actionSystem.HandleTargetClick(null, _siteA); // This sets the internal PendingSite.

            // Act: User selects "Neutral" from the UI
            bool success = _actionSystem.FinalizeSpyReturn(PlayerColor.Neutral);

            // Assert
            Assert.IsTrue(success);
            Assert.HasCount(1, _siteA.Spies);
            Assert.AreEqual(PlayerColor.Blue, _siteA.Spies[0], "Blue spy should remain.");
            Assert.AreEqual(0, _player1.Power, "Power should be deducted now.");
        }

        #endregion

        #region State Management Tests

        [TestMethod]
        public void CancelTargeting_ResetsStateAndPendingCard()
        {
            var card = new Card("c", "c", 0, CardAspect.Shadow, 0, 0);
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);
            _actionSystem.CancelTargeting();
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
            Assert.IsNull(_actionSystem.PendingCard);
        }

        #endregion

        [TestMethod]
        public void HandleTargetClick_Assassinate_Fails_IfPowerLostDuringTargeting()
        {
            // Arrange: 
            // 1. Player starts with enough power (3).
            _player1.Power = 3;

            // 2. Player clicks "Assassinate" button -> Enters Targeting Mode.
            _actionSystem.TryStartAssassinate();
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);

            // 3. EDGE CASE: Something else consumes power before the click happens 
            // (e.g. a triggered event, or a bug in UI logic).
            _player1.Power = 0;

            // Setup a valid target to ensure it's the Power stopping us, not the map logic.
            _node1.Occupant = _player1.Color; // Presence
            _node2.Occupant = _player2.Color; // Enemy

            // Act: Player clicks the target.
            bool success = _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsFalse(success, "Action should fail if power dropped below cost during targeting.");
            Assert.AreEqual(PlayerColor.Blue, _node2.Occupant, "Enemy should still be alive.");
            Assert.AreEqual(0, _player1.Power, "Power should not go negative.");
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_Fails_IfPowerLostDuringTargeting()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.TryStartReturnSpy(); // Enters targeting

            // EDGE CASE: Power is lost
            _player1.Power = 2;

            // Setup valid target
            _node2.Occupant = _player1.Color; // Presence at site
            _siteA.Spies.Add(_player2.Color); // Enemy spy

            // Act
            bool success = _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(success);
            Assert.Contains(_player2.Color, _siteA.Spies, "Spy should remain.");
            Assert.AreEqual(2, _player1.Power, "Power should not decrease.");
        }
    }
}