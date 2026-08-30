using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class AssassinateCommandTests
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
            var command = new AssassinateCommand(targetNodeId: 999);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when target node not found");
        }

        [TestMethod]
        public void Validate_Returns_False_When_CanAssassinate_False()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(false);

            var command = new AssassinateCommand(_targetNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should return false when MapManager rejects the target");
        }

        [TestMethod]
        public void Validate_Returns_True_When_CanAssassinate_True_AndPlayerCanAffordCost()
        {
            // Arrange
            var player = TestData.Players.RedPlayer(); // 10 Power, well above the cost
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(true);

            var command = new AssassinateCommand(_targetNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Validate_Returns_False_When_NoCardId_AndPlayerCannotAffordPowerCost()
        {
            // Arrange: paying with Power (no CardId), but below GameConstants.AssassinatePowerCost.
            // ActionInputController already screens this out before building the command, but
            // Validate() is the "strict server-side validation" step (see CommandDispatcher) and
            // must not trust that a command was only ever built through that UI path.
            var player = TestData.Players.PoorPlayer(); // 0 Power
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(true);

            var command = new AssassinateCommand(_targetNode.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "Should reject an unaffordable Power-paid assassinate");
        }

        [TestMethod]
        public void Validate_IgnoresPowerCost_When_FedByCard()
        {
            // Arrange: a CardId means the cost is paid by devouring/feeding a card, not Power.
            var player = TestData.Players.PoorPlayer(); // 0 Power
            _state.TurnManager.ActivePlayer.Returns(player);
            _state.MapManager.CanAssassinate(_targetNode, player).Returns(true);

            var command = new AssassinateCommand(_targetNode.Id, cardId: "feeding_card");

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result, "A card-fed assassinate shouldn't require spare Power");
        }

        [TestMethod]
        public void Execute_DelegatesTo_ActionSystemPerformAssassinate_WhenValid()
        {
            // Arrange
            // Execute() delegates to ActionSystem.PerformAssassinate (rather than calling
            // MapManager.Assassinate/PlayerStateManager.TrySpendPower/CompleteAction directly)
            // because PerformAssassinate is also where the transactional "Devour a card ->
            // Assassinate" handling lives (DevourCardId) - see planning.txt RESOLVED for why
            // that matters, even though no shipped card exercises it via a live click today.
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);

            var command = new AssassinateCommand(_targetNode.Id, cardId: "feeding_card", devourCardId: "devour_me");

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.Received(1).PerformAssassinate(_targetNode, "feeding_card", "devour_me");
        }

        [TestMethod]
        public void Execute_DoesNothing_When_TargetNode_NotFound()
        {
            // Arrange
            var command = new AssassinateCommand(targetNodeId: 999);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.DidNotReceive().PerformAssassinate(Arg.Any<MapNode>(), Arg.Any<string?>(), Arg.Any<string?>());
        }
    }
}
