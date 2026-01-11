using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
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
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            mockMapManager.Received(1).TryDeploy(mockPlayer, node);
        }
    }
}
