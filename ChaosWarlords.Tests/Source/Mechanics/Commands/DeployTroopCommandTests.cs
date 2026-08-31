using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class DeployTroopCommandTests
    {
        [TestMethod]
        public void Execute_CallsTryDeployOnMapManager()
        {
            // Arrange
            var stateFake = new TestGameplayState();

            var mockMapManager = stateFake.MapManager;
            var mockTurnManager = stateFake.TurnManager;
            var mockPlayer = TestData.Players.RedPlayer();

            mockTurnManager.ActivePlayer.Returns(mockPlayer);

            var node = TestData.MapNodes.Node1();
            mockMapManager.Nodes.Returns(new List<MapNode> { node });
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            mockMapManager.Received(1).TryDeploy(mockPlayer, node);
        }
    }
}
