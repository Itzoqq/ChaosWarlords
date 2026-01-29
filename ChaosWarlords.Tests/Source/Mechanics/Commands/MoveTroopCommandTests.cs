using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class MoveTroopCommandTests
    {
        private TestGameplayState _state = null!;
        private MapNode _sourceNode = null!;
        private MapNode _destNode = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _sourceNode = TestData.MapNodes.Node1();
            _destNode = TestData.MapNodes.Node2();
            
            // Add nodes to the collection
            var nodeList =new List<MapNode> { _sourceNode, _destNode };
            _state.MapManager.Nodes.Returns(nodeList);
        }

        [TestMethod]
        public void Validate_Returns_False_When_SourceNode_NotFound()
        {
            // Arrange: Use invalid source ID
            var command = new MoveTroopCommand(sourceNodeId: 999, destinationNodeId: _destNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when source node not found");
        }

        [TestMethod]
        public void Validate_Returns_False_When_DestinationNode_NotFound()
        {
            // Arrange: Use invalid destination ID
            var command = new MoveTroopCommand(sourceNodeId: _sourceNode.Id, destinationNodeId: 999);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when destination node not found");
        }

        [TestMethod]
        public void Validate_Returns_False_When_BothNodes_NotFound()
        {
            // Arrange: Use both invalid IDs
            var command = new MoveTroopCommand(sourceNodeId: 999, destinationNodeId: 888);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when both nodes not found");
        }

        [TestMethod]
        public void Validate_Returns_False_When_CannotMoveFrom_Source()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanMoveSource(_sourceNode, player).Returns(false);
            _state.MapManager.CanMoveDestination(_destNode).Returns(true);

            var command = new MoveTroopCommand(_sourceNode.Id, _destNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when source validation fails");
        }

        [TestMethod]
        public void Validate_Returns_False_When_CannotMoveTo_Destination()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanMoveSource(_sourceNode, player).Returns(true);
            _state.MapManager.CanMoveDestination(_destNode).Returns(false);

            var command = new MoveTroopCommand(_sourceNode.Id, _destNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when destination validation fails");
        }

        [TestMethod]
        public void Validate_Returns_True_When_AllValidations_Pass()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanMoveSource(_sourceNode, player).Returns(true);
            _state.MapManager.CanMoveDestination(_destNode).Returns(true);

            var command = new MoveTroopCommand(_sourceNode.Id, _destNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result, "Should return true when all validations pass");
        }

        [TestMethod]
        public void Validate_EdgeCase_SameNode_Returns_AsExpected()
        {
            // Arrange: Moving to the same node
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            
            // Mock behavior - MapManager would likely reject this as invalid destination
            _state.MapManager.CanMoveSource(_sourceNode, player).Returns(true);
            _state.MapManager.CanMoveDestination(_sourceNode).Returns(false);

            var command = new MoveTroopCommand(_sourceNode.Id, _sourceNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when trying to move to same node");
        }

        [TestMethod]
        public void Execute_CallsMapManager_MoveTroop_WhenValid()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new MoveTroopCommand(_sourceNode.Id, _destNode.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.MapManager.Received(1).MoveTroop(_sourceNode, _destNode, player);
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_DoesNothing_When_SourceNode_NotFound()
        {
            // Arrange
            var command = new MoveTroopCommand(sourceNodeId: 999, destinationNodeId: _destNode.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.MapManager.DidNotReceive().MoveTroop(Arg.Any<MapNode>(), Arg.Any<MapNode>(), Arg.Any<ChaosWarlords.Source.Entities.Actors.Player>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }
    }
}
