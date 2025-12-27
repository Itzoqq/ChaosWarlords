using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Interfaces;
using ChaosWarlords.Source.Systems;
using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class DeployTroopCommandTests
    {
        [TestMethod]
        public void Execute_CallsTryDeployOnMapManager()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockMapManager = Substitute.For<IMapManager>();
            var mockTurnManager = Substitute.For<ITurnManager>();
            var mockPlayer = new Player(PlayerColor.Red);
            
            mockState.MapManager.Returns(mockMapManager);
            mockState.TurnManager.Returns(mockTurnManager);
            mockTurnManager.ActivePlayer.Returns(mockPlayer);
            
            var node = new MapNode(1, Vector2.Zero);
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(mockState);

            // Assert
            mockMapManager.Received(1).TryDeploy(mockPlayer, node);
        }
    }
}
