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
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using NSubstitute;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class ActionSystemTests
    {
        private Player _player1 = null!;
        private Player _player2 = null!;
        private IMapManager _mapManager = null!; // Mocked dependency
        private ITurnManager _turnManager = null!; // Mocked dependency
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

            // Mock the Managers
            _mapManager = Substitute.For<IMapManager>();
            _turnManager = Substitute.For<ITurnManager>();

            // Configure TurnManager to say Player 1 is active by default
            _turnManager.ActivePlayer.Returns(_player1);

            // Setup Data Entities
            _node1 = new MapNode(1, new Vector2(10, 10));
            _node2 = new MapNode(2, new Vector2(20, 10));

            // Correct Constructor (Name, ControlRes, Amount, TotalControlRes, Amount)
            _siteA = new NonCitySite("SiteA", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);

            // Inject the mock
            _actionSystem = new ActionSystem(_turnManager, _mapManager);

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

        #region 4. Edge Cases & Failures

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
        public void HandleTargetClick_ReturnSpy_DoesNotSpendPower_IfMapManagerRejects_Regression()
        {

            // Arrange
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Mock: Spies exist, BUT ReturnSpecificSpy returns FALSE (e.g. no presence)
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue });
            _mapManager.ReturnSpecificSpy(Arg.Any<Site>(), Arg.Any<Player>(), Arg.Any<PlayerColor>()).Returns(false);

            // Act
            _actionSystem.HandleTargetClick(null, _siteA);

            // Assert
            Assert.IsFalse(_eventCompletedFired, "Action should not complete.");
            Assert.IsTrue(_eventFailedFired, "Action should fail."); // Currently might not fire if logic is flawed
            Assert.AreEqual(3, _player1.Power, "Power should NOT be spent if action failed.");
        }

        [TestMethod]
        public void HandleTargetClick_Assassinate_DoesNotSpendPower_IfMapManagerRejects_Regression()
        {
            // Arrange
            _player1.Power = 3;
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate);

            // Mock: Invalid target according to Manager (e.g. protected, or logic mismatch)
            _mapManager.CanAssassinate(_node2, _player1).Returns(false);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventFailedFired);
            Assert.AreEqual(3, _player1.Power, "Power should NOT be spent.");
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

        #region 5. Card-Based Action Tests (Restored)

        [TestMethod]
        public void HandleTargetClick_Assassinate_ViaCard_DoesNotSpendPower()
        {
            // Arrange
            var card = new Card("kill_card", "Assassin", 0, CardAspect.Sorcery, 0, 0, 0);

            // Start targeting WITH a pending card
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            // Set Power to 0 to ensure it doesn't try to spend any (and doesn't fail)
            _player1.Power = 0;
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(0, _player1.Power); // Power should remain 0
            _mapManager.Received(1).Assassinate(_node2, _player1);
            Assert.IsNull(_actionSystem.PendingCard); // Card should be cleared after action
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_ViaCard_DoesNotSpendPower()
        {
            // Arrange
            var card = new Card("spy_card", "Spy Master", 0, CardAspect.Shadow, 0, 0, 0);
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy, card);

            _player1.Power = 0;
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
        public void FinalizeSpyReturn_ViaCard_DoesNotSpendPower()
        {
            // Arrange
            var card = new Card("spy_card", "Spy Master", 0, CardAspect.Shadow, 0, 0, 0);
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy, card);

            // Setup Ambiguity to force the 'Finalize' path
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });

            // Initial Click
            _actionSystem.HandleTargetClick(null, _siteA);
            _player1.Power = 0; // Ensure no power needed for step 2

            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue).Returns(true);

            // Act
            _actionSystem.FinalizeSpyReturn(PlayerColor.Blue);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue);
        }

        [TestMethod]
        public void HandleTargetClick_Supplant_ViaCard_Succeeds()
        {
            // Arrange
            var card = new Card("supplant_card", "Overlord", 0, CardAspect.Warlord, 0, 0, 0);
            _actionSystem.StartTargeting(ActionState.TargetingSupplant, card);

            _player1.TroopsInBarracks = 1;
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            _mapManager.Received(1).Supplant(_node2, _player1);
        }

        #endregion

        #region 6. Move Unit Tests (Two-Step Action)

        [TestMethod]
        public void HandleTargetClick_MoveSource_TransitionsToDestination_OnValidTarget()
        {
            // Arrange
            var card = new Card("move_card", "Displacer", 0, CardAspect.Order, 0, 0, 0);
            _actionSystem.StartTargeting(ActionState.TargetingMoveSource, card);

            // Mock: MapManager says this node is a valid source (Enemy + Presence)
            _mapManager.CanMoveSource(_node1, _player1).Returns(true);

            // Act
            _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.AreEqual(ActionState.TargetingMoveDestination, _actionSystem.CurrentState, "Should transition to Step 2");
            Assert.AreEqual(_node1, _actionSystem.PendingMoveSource, "Should store the source node");
            Assert.IsFalse(_eventCompletedFired, "Action is not done yet");
        }

        [TestMethod]
        public void HandleTargetClick_MoveSource_Fails_OnInvalidTarget()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingMoveSource);
            _mapManager.CanMoveSource(_node1, _player1).Returns(false); // Invalid

            // Act
            _actionSystem.HandleTargetClick(_node1, null);

            // Assert
            Assert.IsTrue(_eventFailedFired, "Should fire failure event");
            Assert.AreEqual(ActionState.TargetingMoveSource, _actionSystem.CurrentState, "Should remain in Step 1");
        }

        [TestMethod]
        public void HandleTargetClick_MoveDestination_CompletesAction_OnValidTarget()
        {
            // Arrange: Set up state as if Step 1 just finished
            var card = new Card("move_card", "Displacer", 0, CardAspect.Order, 0, 0, 0);
            _actionSystem.StartTargeting(ActionState.TargetingMoveSource, card);

            // Perform Step 1 manually to set internal state
            _mapManager.CanMoveSource(_node1, _player1).Returns(true);
            _actionSystem.HandleTargetClick(_node1, null);

            // Mock Step 2 checks
            _mapManager.CanMoveDestination(_node2).Returns(true);

            // Act: Step 2 (Select Destination)
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventCompletedFired, "Action should complete");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "State should reset to Normal");
            _mapManager.Received(1).MoveTroop(_node1, _node2, Arg.Any<Player>()); // Verify logic was called
        }

        [TestMethod]
        public void HandleTargetClick_MoveDestination_Fails_OnOccupiedTarget()
        {
            // Arrange: Manually advance to Step 2
            _actionSystem.StartTargeting(ActionState.TargetingMoveSource);
            _mapManager.CanMoveSource(_node1, _player1).Returns(true);
            _actionSystem.HandleTargetClick(_node1, null);

            // Mock Step 2 check (Target is occupied)
            _mapManager.CanMoveDestination(_node2).Returns(false);

            // Act
            _actionSystem.HandleTargetClick(_node2, null);

            // Assert
            Assert.IsTrue(_eventFailedFired);
            Assert.AreEqual(ActionState.TargetingMoveDestination, _actionSystem.CurrentState, "Should stay in Step 2 to allow retry");
            _mapManager.DidNotReceive().MoveTroop(Arg.Any<MapNode>(), Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        #endregion

        [TestMethod]
        public void TryStartDevourHand_SetsState_ToTargetingDevourHand()
        {
            // Arrange
            var sourceCard = new Card("eater", "Eater of Souls", 0, CardAspect.Sorcery, 0, 0, 0);
            _player1.Hand.Add(new Card("food", "Food", 0, CardAspect.Neutral, 0, 0, 0)); // Ensure hand is not empty

            // Act
            _actionSystem.TryStartDevourHand(sourceCard);

            // Assert
            Assert.AreEqual(ActionState.TargetingDevourHand, _actionSystem.CurrentState);
            Assert.AreEqual(sourceCard, _actionSystem.PendingCard);
        }

        [TestMethod]
        public void TryStartDevourHand_CompletesImmediately_IfHandIsEmpty()
        {
            // Arrange
            var sourceCard = new Card("eater", "Eater", 0, CardAspect.Sorcery, 0, 0, 0);
            _player1.Hand.Clear(); // Empty hand

            // Listen for completion
            bool completed = false;
            _actionSystem.OnActionCompleted += (s, e) => completed = true;

            // Act
            _actionSystem.TryStartDevourHand(sourceCard);

            // Assert
            Assert.IsTrue(completed, "Should fire OnActionCompleted immediately if there is nothing to devour.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "State should remain Normal.");
        }
    }
}


