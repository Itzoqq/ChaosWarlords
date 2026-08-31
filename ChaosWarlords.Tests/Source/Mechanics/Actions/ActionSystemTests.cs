using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]

    [TestCategory("Unit")]
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
            Utilities.TestLogger.Initialize();
            _player1 = TestData.Players.RedPlayer();
            _player1.SpendPower(_player1.Power);
            _player1.SpendInfluence(_player1.Influence);

            _player2 = TestData.Players.BluePlayer();
            _player2.SpendPower(_player2.Power);
            _player2.SpendInfluence(_player2.Influence);

            // Mock the Managers
            _mapManager = Substitute.For<IMapManager>();
            _turnManager = Substitute.For<ITurnManager>();

            // Configure TurnManager to say Player 1 is active by default
            _turnManager.ActivePlayer.Returns(_player1);

            // Setup Data Entities
            _node1 = TestData.MapNodes.Node1();
            _node2 = TestData.MapNodes.Node2();
            _siteA = TestData.Sites.NeutralSite();
            _siteA.Id = 10;

            // Mock Nodes and Sites collections
            _mapManager.Nodes.Returns(new List<MapNode> { _node1, _node2 });
            _mapManager.Sites.Returns(new List<Site> { _siteA });

            // Inject the mock
            _actionSystem = new ActionSystem(_turnManager, _mapManager, Utilities.TestLogger.Instance);
            var playerStateManager = Substitute.For<IPlayerStateManager>();
            playerStateManager.TrySpendPower(Arg.Any<Player>(), Arg.Any<int>())
                .Returns(x =>
                {
                    Player p = (Player)x[0];
                    int amt = (int)x[1];
                    if (p.Power >= amt)
                    {
                        // Use SpendPower instead of direct set
                        return p.SpendPower(amt); 
                    }
                    return false;
                });
            _actionSystem.SetPlayerStateManager(playerStateManager);

            // Subscribe to events for every test
            _eventCompletedFired = false;
            _eventFailedFired = false;
            _actionSystem.OnActionCompleted += (s, e) => _eventCompletedFired = true;
            _actionSystem.OnActionFailed += (s, msg) => _eventFailedFired = true;
            // CRITICAL: Ensure auto-executed commands are actually run in the test environment
            _actionSystem.OnAutoExecuteCommand += (cmd) => ExecuteIfNotNull(cmd);

            // Reset player defaults
            _player1.AddPower(10);
            _player1.TroopsInBarracks = 10;
            _player1.SpiesInBarracks = 5;
        }

        private void ExecuteIfNotNull(IGameCommand? cmd)
        {
            var stateFake = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateFake.MapManager = _mapManager;
            stateFake.ActionSystem = _actionSystem;
            stateFake.TurnManager = _turnManager;

            // Re-build MatchContext to use these updated dependencies
            stateFake.InitializeMatchContext();

            cmd?.Execute(stateFake.MatchContext);
        }

        #region 1. Initiation Tests


        [TestMethod]
        [DataRow("Assassinate", 3, 3, true, ActionState.TargetingAssassinate)]
        [DataRow("Assassinate", 3, 2, false, ActionState.Normal)]
        [DataRow("ReturnSpy", 3, 3, true, ActionState.TargetingReturnSpy)]
        [DataRow("ReturnSpy", 3, 2, false, ActionState.Normal)]
        public void TryStartAction_ChecksPowerRequirement(
            string actionName,
            int requiredPower,
            int playerPower,
            bool shouldSucceed,
            ActionState expectedState)
        {
            _player1.SpendPower(_player1.Power); // Reset to 0
            _player1.AddPower(playerPower);
            _eventFailedFired = false; // Reset

            // Setup map validation for Return Spy (required after bug fix)
            if (actionName == "ReturnSpy")
            {
                _mapManager.HasValidReturnSpyTarget(_player1).Returns(shouldSucceed);
            }

            if (actionName == "Assassinate")
                _actionSystem.TryStartAssassinate();
            else
                _actionSystem.TryStartReturnSpy();

            Assert.AreEqual(expectedState, _actionSystem.CurrentState);
            Assert.AreEqual(!shouldSucceed, _eventFailedFired,
                shouldSucceed
                    ? "Should not fire failure event when power is sufficient"
                    : "Should fire failure event when power is insufficient");
        }


        #endregion

        #region 2. Basic Execution Tests

        [TestMethod]
        public void HandleTargetClick_Assassinate_PaysCost_AndCallsMapManager()
        {
            // Arrange
            _player1.AddPower(3);
            _actionSystem.TryStartAssassinate();
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            var cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(10, _player1.Power); // Cost paid (13 - 3 = 10)
            _mapManager.Received(1).Assassinate(_node2, _player1);
        }

        [TestMethod]
        public void HandleTargetClick_Assassinate_InvalidTarget_Fails()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate);
            _mapManager.CanAssassinate(_node2, _player1).Returns(false);

            // Act
            var cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd);

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
            var cmd = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd);

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
            var cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd);

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
            // ActionInputController.HandleReturn now delegates to MapManager.CanReturnTroop
            // (the single authoritative check) rather than reimplementing the Occupant/
            // Presence checks itself - see planning.txt.
            _mapManager.CanReturnTroop(_node1, _player1).Returns(true);

            // Act
            var cmd = _actionSystem.HandleTargetClick(_node1, null!);
            ExecuteIfNotNull(cmd);

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
            _player1.AddPower(3);
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Mock: Only Blue spies here
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue });
            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue).Returns(true);

            // Act
            var cmd = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(10, _player1.Power);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue);
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_DetectsAmbiguity_MultipleFactions()
        {
            // Arrange
            _player1.AddPower(3);
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Mock: Blue AND Neutral spies here
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });

            // Act
            var cmd = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd);

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
            _player1.AddPower(3);
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Set up ambiguity to set PendingSite
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });
            _actionSystem.HandleTargetClick(null!, _siteA);

            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Neutral).Returns(true);

            // Act
            var cmd = _actionSystem.FinalizeSpyReturn(PlayerColor.Neutral);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(10, _player1.Power);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Neutral);
        }

        #endregion

        #region 4. Edge Cases & Failures

        [TestMethod]
        public void HandleTargetClick_Assassinate_Fails_IfPowerLostDuringTargeting()
        {
            // Arrange
            _player1.AddPower(3);
            _actionSystem.TryStartAssassinate();
            // We cannot set Power to 0 directly anymore. 
            // We could spend it or create a new player. 
            // Assuming SpendPower works even if it drains all? Yes.
            // Or just new up the player. But dependencies are linked.
            // _player1.SpendPower(_player1.Power); -> Cleanest way to zero out.
            _player1.SpendPower(_player1.Power); // Lost power while targeting (e.g. interruption)

            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            var cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            Assert.IsTrue(_eventFailedFired);
            _mapManager.DidNotReceive().Assassinate(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_ReturnSpy_DoesNotSpendPower_IfMapManagerRejects_Regression()
        {

            // Arrange
            _player1.AddPower(3);
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Mock: Spies exist, BUT ReturnSpecificSpy returns FALSE (e.g. no presence)
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue });
            _mapManager.ReturnSpecificSpy(Arg.Any<Site>(), Arg.Any<Player>(), Arg.Any<PlayerColor>()).Returns(false);

            // Act
            var cmd = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsFalse(_eventCompletedFired, "Action should not complete.");
            Assert.IsTrue(_eventFailedFired, "Action should fail."); // Currently might not fire if logic is flawed
            Assert.AreEqual(13, _player1.Power, "Power should NOT be spent if action failed.");
        }

        [TestMethod]
        public void HandleTargetClick_Assassinate_DoesNotSpendPower_IfMapManagerRejects_Regression()
        {
            // Arrange
            _player1.AddPower(3);
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate);

            // Mock: Invalid target according to Manager (e.g. protected, or logic mismatch)
            _mapManager.CanAssassinate(_node2, _player1).Returns(false);

            // Act
            var cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsTrue(_eventFailedFired);
            Assert.AreEqual(13, _player1.Power, "Power should NOT be spent.");
        }


        [TestMethod]
        public void HandleTargetClick_PlaceSpy_Fails_IfSpyAlreadyThere()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingPlaceSpy);
            _siteA.Spies.Add(_player1.Color); // Already have a spy here

            // Act
            var cmd = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd);

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
            var cmd = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd);

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
            var cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsFalse(_eventCompletedFired);
            _mapManager.DidNotReceive().Supplant(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void HandleTargetClick_Return_Fails_WhenMapManagerRejectsIt()
        {
            // ActionInputController.HandleReturn now delegates the Neutral/unoccupied/
            // Presence checks entirely to MapManager.CanReturnTroop (see planning.txt) -
            // that logic is tested directly against a real MapManager in MapManagerTests.cs.
            // This test only confirms HandleTargetClick correctly propagates a false
            // CanReturnTroop result into "no command, action not completed" - configuring
            // CanReturnTroop explicitly (rather than relying on the mock's default false) so
            // it can't be confused with a test that just happens to pass either way.
            _actionSystem.StartTargeting(ActionState.TargetingReturn);
            _node1.Occupant = PlayerColor.Neutral;
            _mapManager.CanReturnTroop(_node1, _player1).Returns(false);

            // Act
            var cmd = _actionSystem.HandleTargetClick(_node1, null!);
            ExecuteIfNotNull(cmd);

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
            var card = TestData.Cards.CheapCard();

            // Start targeting WITH a pending card
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            // Set Power to 0 to ensure it doesn't try to spend any (and doesn't fail)
            // _player1.Power = 0; -> Replaced with SpendPower logic if non-zero, or just ensure it starts at 0.
            if (_player1.Power > 0) _player1.SpendPower(_player1.Power); // Ensure 0
            // Actually Setup sets it to 10? Yes. So we must clear it.
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            var cmd1 = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd1);

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
            var card = TestData.Cards.CheapCard();
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy, card);

            if (_player1.Power > 0) _player1.SpendPower(_player1.Power);
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue });
            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue).Returns(true);

            // Act
            var cmd2 = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd2);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            Assert.AreEqual(0, _player1.Power);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue);
        }

        [TestMethod]
        public void FinalizeSpyReturn_ViaCard_DoesNotSpendPower()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy, card);

            // Setup Ambiguity to force the 'Finalize' path
            _mapManager.GetEnemySpiesAtSite(_siteA, _player1).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });

            // Initial Click
            var cmd3 = _actionSystem.HandleTargetClick(null!, _siteA);
            ExecuteIfNotNull(cmd3);
            if (_player1.Power > 0) _player1.SpendPower(_player1.Power); // Ensure no power needed for step 2

            _mapManager.ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue).Returns(true);

            // Act
            var cmd = _actionSystem.FinalizeSpyReturn(PlayerColor.Blue);
            ExecuteIfNotNull(cmd);

            // Assert
            Assert.IsTrue(_eventCompletedFired);
            _mapManager.Received(1).ReturnSpecificSpy(_siteA, _player1, PlayerColor.Blue);
        }

        [TestMethod]
        public void HandleTargetClick_Supplant_ViaCard_Succeeds()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            _actionSystem.StartTargeting(ActionState.TargetingSupplant, card);

            _player1.TroopsInBarracks = 1;
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act
            var cmd4 = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd4);

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
            var card = TestData.Cards.MoveUnitCard();
            _actionSystem.StartTargeting(ActionState.TargetingMoveSource, card);

            // Mock: MapManager says this node is a valid source (Enemy + Presence)
            _mapManager.CanMoveSource(_node1, _player1).Returns(true);

            // Act
            var cmd5 = _actionSystem.HandleTargetClick(_node1, null!);
            ExecuteIfNotNull(cmd5);

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
            var cmd6 = _actionSystem.HandleTargetClick(_node1, null!);
            ExecuteIfNotNull(cmd6);

            // Assert
            Assert.IsTrue(_eventFailedFired, "Should fire failure event");
            Assert.AreEqual(ActionState.TargetingMoveSource, _actionSystem.CurrentState, "Should remain in Step 1");
        }

        [TestMethod]
        public void HandleTargetClick_MoveDestination_CompletesAction_OnValidTarget()
        {
            // Arrange: Set up state as if Step 1 just finished
            var card = TestData.Cards.MoveUnitCard();
            _actionSystem.StartTargeting(ActionState.TargetingMoveSource, card);

            // Perform Step 1 manually to set internal state
            _mapManager.CanMoveSource(_node1, _player1).Returns(true);
            var step1Cmd = _actionSystem.HandleTargetClick(_node1, null!);
            ExecuteIfNotNull(step1Cmd);

            // Mock Step 2 checks
            _mapManager.CanMoveDestination(_node2).Returns(true);

            // Act: Step 2 (Select Destination)
            var step2Cmd = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(step2Cmd);

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
            var cmd1 = _actionSystem.HandleTargetClick(_node1, null!);
            ExecuteIfNotNull(cmd1);

            // Mock Step 2 check (Target is occupied)
            _mapManager.CanMoveDestination(_node2).Returns(false);

            // Act
            var cmd2 = _actionSystem.HandleTargetClick(_node2, null!);
            ExecuteIfNotNull(cmd2);

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
            var sourceCard = TestData.Cards.DevourCard();
            _player1.AddToHand(TestData.Cards.CheapCard()); // Ensure hand is not empty

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
            var sourceCard = TestData.Cards.DevourCard();
            _player1.ClearHand(); // Empty hand

            // Listen for completion
            bool completed = false;
            _actionSystem.OnActionCompleted += (s, e) => completed = true;

            // Act
            _actionSystem.TryStartDevourHand(sourceCard);

            // Assert
            Assert.IsTrue(completed, "Should fire OnActionCompleted immediately if there is nothing to devour.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "State should remain Normal.");
        }
        [TestMethod]
        public void TryStartDevourHand_WithSkippedTarget_DoesNotInvokeCallback()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            bool callbackInvoked = false;
            Action callback = () => callbackInvoked = true;

            // Set Pre-Target to Skipped
            _actionSystem.SetPreTarget(sourceCard, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            // Act
            _actionSystem.TryStartDevourHand(sourceCard, callback);

            // Assert
            Assert.IsFalse(callbackInvoked, "Callback should NOT be invoked when action is skipped (Cost not paid -> Reward not given).");
            Assert.IsNull(_actionSystem.GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourHand), "PreTarget should be cleared.");
        }

        [TestMethod]
        public void StartTargeting_ConsumesPreTarget_Regression_PreventZombieTargets()
        {
            // Arrange
            var card = TestData.Cards.AssassinCard();
            _actionSystem.SetPreTarget(card, ActionState.TargetingAssassinate, _node2);
            _mapManager.CanAssassinate(_node2, _player1).Returns(true);

            // Act 1: First Play (Should Auto-Execute)
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            // Assert 1: Should have executed
            _mapManager.Received(1).Assassinate(_node2, _player1);
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "State should be Normal after auto-execution");

            // Reset mocks/events for second run
            _mapManager.ClearReceivedCalls();
            bool completed = false;
            _actionSystem.OnActionCompleted += (s, e) => completed = true;

            // Act 2: Second Play (Should NOT Auto-Execute)
            // If the zombie target bug exists, this would fire immediately.
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            // Assert 2: Should be waiting for input
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState, "Should be waiting for input on second play");
            _mapManager.DidNotReceive().Assassinate(Arg.Any<MapNode>(), Arg.Any<Player>());
            Assert.IsFalse(completed, "Action should not have completed automatically");

        }

        #region 7. Pre-Target Execution Tests (CRAP Reduction)

        [TestMethod]
        public void TryStartSupplant_WithMapNodePreTarget_ExecutesImmediately()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            _actionSystem.SetPreTarget(card, ActionState.TargetingSupplant, _node2);
            // Ensure map checks pass implicitly or are skipped by direct execution? 
            // Looking at TryExecuteSupplantPreTarget, it calls PerformSupplant DIRECTLY, skipping validation?
            // Wait, TryExecuteSupplantPreTarget implementation:
            // if (targetNode != null) { PerformSupplant(...); return true; }
            // So it SKIPS "CanStartSupplant" validation if pre-target exists? 
            // Need to verify logic. Usually PreTargets are assumed valid because they come from valid sources (Replay/AI).

            // Act
            _actionSystem.TryStartSupplant(card);

            // Assert
            _mapManager.Received(1).Supplant(_node2, _player1);
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "State should be Normal after auto-execution");
        }

        [TestMethod]
        public void TryStartSupplant_WithNodeIdPreTarget_FindsNodeAndExecutes()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            // _node2 ID is usually hardcoded in TestData or we rely on object equality. 
            // TestData.MapNodes.Node2().Id is usually 2.
            int targetId = _node2.Id;
            _actionSystem.SetPreTarget(card, ActionState.TargetingSupplant, targetId);

            // Act
            _actionSystem.TryStartSupplant(card);

            // Assert
            _mapManager.Received(1).Supplant(_node2, _player1);
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void TryStartSupplant_WithNullPreTarget_FallsBackToTargeting()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            // No pre-target set

            // Mock validation to allow start
            _player1.TroopsInBarracks = 1;
            _mapManager.HasValidAssassinationTarget(_player1).Returns(true);

            // Act
            _actionSystem.TryStartSupplant(card);

            // Assert
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState, "Should enter targeting mode");
            _mapManager.DidNotReceive().Supplant(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        [TestMethod]
        public void TryStartSupplant_WithInvalidPreTargetType_FallsBackToTargeting()
        {
            // Arrange
            var card = TestData.Cards.SupplantCard();
            _actionSystem.SetPreTarget(card, ActionState.TargetingSupplant, "InvalidObject");

            // Mock validation to allow start
            _player1.TroopsInBarracks = 1;
            _mapManager.HasValidAssassinationTarget(_player1).Returns(true);

            // Act
            _actionSystem.TryStartSupplant(card);

            // Assert
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState, "Should ignore invalid target and enter targeting mode");
            _mapManager.DidNotReceive().Supplant(Arg.Any<MapNode>(), Arg.Any<Player>());
        }

        #endregion

        #region 8. PerformAssassinate Tests (CRAP Reduction)

        [TestMethod]
        public void PerformAssassinate_WithoutCardPayment_SpendsAssassinateCost()
        {
            // Arrange
            _player1.AddPower(GameConstants.AssassinatePowerCost);
            _node2.Occupant = PlayerColor.Blue; // Enemy target
            var matchManager = Substitute.For<IMatchManager>();
            _actionSystem.SetMatchManager(matchManager);

            // Act
            _actionSystem.PerformAssassinate(_node2, cardId: null, devourCardId: null);

            // Assert
            Assert.AreEqual(10, _player1.Power, "Should spend assassinate cost");
            _mapManager.Received(1).Assassinate(_node2, _player1);
            Assert.IsTrue(_eventCompletedFired, "Should complete action");
        }

        [TestMethod]
        public void PerformAssassinate_WithCardPayment_DoesNotSpendPower()
        {
            // Arrange
            var card = TestData.Cards.AssassinCard();
            _player1.SpendPower(_player1.Power); // Set to 0
            _node2.Occupant = PlayerColor.Blue;

            // Act
            _actionSystem.PerformAssassinate(_node2, cardId: card.Id, devourCardId: null);

            // Assert
            Assert.AreEqual(0, _player1.Power, "Should not spend power when paid with card");
            _mapManager.Received(1).Assassinate(_node2, _player1);
            Assert.IsTrue(_eventCompletedFired);
        }

        [TestMethod]
        public void PerformAssassinate_WithDevourCard_DevoursCardFirst()
        {
            // Arrange
            var cardToDevour = TestData.Cards.CheapCard();
            _player1.AddToHand(cardToDevour);
            _player1.AddPower(GameConstants.AssassinatePowerCost);
            _node2.Occupant = PlayerColor.Blue;
            
            var matchManager = Substitute.For<IMatchManager>();
            _actionSystem.SetMatchManager(matchManager);

            // Act
            _actionSystem.PerformAssassinate(_node2, cardId: null, devourCardId: cardToDevour.Id);

            // Assert
            matchManager.Received(1).DevourCard(cardToDevour);
            _mapManager.Received(1).Assassinate(_node2, _player1);
            Assert.IsTrue(_eventCompletedFired);
        }

        [TestMethod]
        public void PerformAssassinate_WithBothCardPaymentAndDevour_DevoursAndDoesNotSpendPower()
        {
            // Arrange
            var paymentCard = TestData.Cards.AssassinCard();
            var devourCard = TestData.Cards.CheapCard();
            _player1.AddToHand(devourCard);
            _player1.SpendPower(_player1.Power); // Set to 0
            _node2.Occupant = PlayerColor.Blue;
            
            var matchManager = Substitute.For<IMatchManager>();
            _actionSystem.SetMatchManager(matchManager);

            // Act
            _actionSystem.PerformAssassinate(_node2, cardId: paymentCard.Id, devourCardId: devourCard.Id);

            // Assert
            matchManager.Received(1).DevourCard(devourCard);
            Assert.AreEqual(0, _player1.Power, "Should not spend power with card payment");
            _mapManager.Received(1).Assassinate(_node2, _player1);
            Assert.IsTrue(_eventCompletedFired);
        }

        [TestMethod]
        public void PerformAssassinate_CallsMapManagerCorrectly()
        {
            // Arrange
            _player1.AddPower(GameConstants.AssassinatePowerCost);
            _node2.Occupant = PlayerColor.Blue;

            // Act
            _actionSystem.PerformAssassinate(_node2, cardId: null, devourCardId: null);

            // Assert
            _mapManager.Received(1).Assassinate(_node2, _player1);
            _mapManager.Received(1).Assassinate(
                Arg.Is<MapNode>(n => n == _node2),
                Arg.Is<Player>(p => p == _player1));
        }

        [TestMethod]
        public void PerformAssassinate_CompletesAction_AfterExecution()
        {
            // Arrange
            _player1.AddPower(GameConstants.AssassinatePowerCost);
            _node2.Occupant = PlayerColor.Blue;
            _eventCompletedFired = false;

            // Act
            _actionSystem.PerformAssassinate(_node2, cardId: null, devourCardId: null);

            // Assert
            Assert.IsTrue(_eventCompletedFired, "Should fire OnActionCompleted event");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "Should return to Normal state");
        }

        #endregion
    }
}
