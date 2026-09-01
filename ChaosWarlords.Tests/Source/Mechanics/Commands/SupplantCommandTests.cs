using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class SupplantCommandTests
    {
        private TestGameplayState _state = null!;
        private MapNode _targetNode = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _targetNode = TestData.MapNodes.Node1();
            _state.MapManager.Nodes.Returns(new List<MapNode> { _targetNode });
        }

        [TestMethod]
        public void Validate_Returns_False_When_TargetNode_NotFound()
        {
            // Arrange
            var command = new SupplantCommand(targetNodeId: 999);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when target node not found");
        }

        [TestMethod]
        public void Validate_Returns_False_When_CanAssassinate_False()
        {
            // Arrange
            var player = TestData.Players.RedPlayer(); // has troops in barracks
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(false);

            var command = new SupplantCommand(_targetNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Validate_Returns_True_When_NoTroopsInBarracks()
        {
            // Arrange: Supplant = Assassinate + Deploy - rulebook p.12/22's "gain 1 VP
            // instead" clause (cross-referenced by "Supplant a Troop," p.14) means an empty
            // barracks doesn't invalidate the command; the assassinate half still happens
            // and the deploy half grants 1 VP instead of placing a troop.
            var player = TestData.Players.PoorPlayer(); // 0 troops in barracks
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(true);

            var command = new SupplantCommand(_targetNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result, "Should accept a supplant with no troops in barracks - deploy half grants 1 VP instead");
        }

        [TestMethod]
        public void Validate_Returns_True_When_CanAssassinate_True_AndTroopsAvailable()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(true);

            var command = new SupplantCommand(_targetNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Execute_DelegatesTo_ActionSystemPerformSupplant_WhenValid()
        {
            // Arrange
            // Execute() delegates to ActionSystem.PerformSupplant (rather than calling
            // MapManager.Supplant/CompleteAction directly) because PerformSupplant is also
            // where the transactional "Devour a card -> Supplant" handling lives
            // (DevourCardId, e.g. the Wight card) - see planning.txt RESOLVED.
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new SupplantCommand(_targetNode.Id, cardId: "wight", devourCardId: "devour_me");

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.Received(1).PerformSupplant(_targetNode, "wight", "devour_me");
        }

        [TestMethod]
        public void Execute_DoesNothing_When_TargetNode_NotFound()
        {
            // Arrange
            var command = new SupplantCommand(targetNodeId: 999);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.DidNotReceive().PerformSupplant(Arg.Any<MapNode>(), Arg.Any<string?>(), Arg.Any<string?>());
        }
    }
}
