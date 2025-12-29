using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using NSubstitute;

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
            var mockState = Substitute.For<IGameplayState>();
            var mockMapManager = Substitute.For<IMapManager>();
            var mockTurnManager = Substitute.For<ITurnManager>();
            var mockPlayer = TestData.Players.RedPlayer();

            mockState.MapManager.Returns(mockMapManager);
            mockState.TurnManager.Returns(mockTurnManager);
            mockTurnManager.ActivePlayer.Returns(mockPlayer);

            var node = TestData.MapNodes.Node1();
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(mockState);

            // Assert
            mockMapManager.Received(1).TryDeploy(mockPlayer, node);
        }
    }
}



