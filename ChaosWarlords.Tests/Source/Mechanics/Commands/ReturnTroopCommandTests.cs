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
        public void Validate_Returns_False_When_NodeUnoccupied()
        {
            // Arrange
            var node = TestData.MapNodes.EmptyNode(); // Occupant == None
            SetNodes(node);
            var command = new ReturnTroopCommand(node.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Validate_Returns_False_When_NodeOccupiedByNeutral()
        {
            // Arrange: Neutral (white) troops cannot be "returned" via this action.
            var node = new MapNodeBuilder().WithId(1).OccupiedBy(PlayerColor.Neutral).Build();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.HasPresence(node, player.Color).Returns(true);

            var command = new ReturnTroopCommand(node.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should reject returning a Neutral-occupied node");
        }

        [TestMethod]
        public void Validate_Returns_False_When_RequesterHasNoPresence()
        {
            // Arrange: mirrors ActionInputController.HandleReturn - contesting a node requires
            // presence there (directly or adjacent).
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.HasPresence(node, player.Color).Returns(false);

            var command = new ReturnTroopCommand(node.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Validate_Returns_True_When_OccupiedByEnemy_AndRequesterHasPresence()
        {
            // Arrange
            var node = TestData.MapNodes.BlueNode();
            SetNodes(node);
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.HasPresence(node, player.Color).Returns(true);

            var command = new ReturnTroopCommand(node.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
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
