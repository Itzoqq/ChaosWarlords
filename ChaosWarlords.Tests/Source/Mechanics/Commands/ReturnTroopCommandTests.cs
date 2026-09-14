using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class ReturnTroopCommandTests
    {
        private TestGameplayState _state = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();

            // Return Troop has no basic-action shape at all - only ever legitimately
            // dispatched while a card-granted Return Unit/ReturnUnitOrSpy effect is the
            // pending targeting state (see Validate's own CurrentState gate, planning.txt
            // TIER 1 item 15). Configured here so every other test in this file doesn't have
            // to repeat it.
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingReturn);
        }

        private void SetNodes(params MapNode[] nodes) => _state.MapManager.Nodes.Returns(nodes.ToList());

        [TestMethod]
        public void Validate_Returns_False_When_TargetNode_NotFound()
        {
            // Arrange
            SetNodes(TestData.MapNodes.EmptyNode());
            var command = new ReturnTroopCommand(targetNodeId: 999);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when target node not found");
        }

        [TestMethod]
        public void Validate_DelegatesTo_MapManagerCanReturnTroop_AndReturnsItsResult_True()
        {
            // Validate() delegates entirely to MapManager.CanReturnTroop (the single
            // authoritative check - see CanReturnTroop's own doc comment for the actual rules
            // logic, tested directly against a real MapManager in MapManagerTests.cs). These
            // tests below verify only the delegation itself - with a bare mocked IMapManager,
            // an unconfigured CanReturnTroop() call returns false by default, so a test
            // asserting Validate() == false without configuring CanReturnTroop can't actually
            // tell correct delegation from "the mock happened to default to false" - configure
            // it explicitly both ways instead.
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanReturnTroop(node, player).Returns(true);

            var command = new ReturnTroopCommand(node.Id);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_DelegatesTo_MapManagerCanReturnTroop_AndReturnsItsResult_False()
        {
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanReturnTroop(node, player).Returns(false);

            var command = new ReturnTroopCommand(node.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenCurrentStateIsTargetingReturnUnitOrSpy()
        {
            // The Intellect Devourer target-type-union state must be accepted too, not just
            // the plain ReturnUnit state.
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanReturnTroop(node, player).Returns(true);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingReturnUnitOrSpy);

            var command = new ReturnTroopCommand(node.Id);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNoReturnTroopEffectIsPending()
        {
            // Adversarial: Return Troop is never a standalone basic action - a directly-
            // dispatched attempt outside its owning targeting state must be rejected even
            // when the underlying map state would otherwise allow it. planning.txt TIER 1
            // item 15.
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanReturnTroop(node, player).Returns(true);
            _state.ActionSystem.CurrentState.Returns(ActionState.Normal);

            var command = new ReturnTroopCommand(node.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_CallsMapManager_ReturnTroop_AndCompletesAction_WhenValid()
        {
            // Arrange
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new ReturnTroopCommand(node.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.MapManager.Received(1).ReturnTroop(node, player);
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_DoesNothing_When_TargetNode_NotFound()
        {
            // Arrange
            SetNodes(TestData.MapNodes.EmptyNode());
            var command = new ReturnTroopCommand(targetNodeId: 999);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.MapManager.DidNotReceive().ReturnTroop(Arg.Any<MapNode>(), Arg.Any<ChaosWarlords.Source.Entities.Actors.Player>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }
    }
}
