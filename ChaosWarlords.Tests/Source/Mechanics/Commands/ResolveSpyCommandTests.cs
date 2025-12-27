using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class ResolveSpyCommandTests
    {
        [TestMethod]
        public void Execute_CallsFinalizeSpyReturnOnActionSystem()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockState.ActionSystem.Returns(mockActionSystem);
            var command = new ResolveSpyCommand(PlayerColor.Blue);

            // Act
            command.Execute(mockState);

            // Assert
            mockActionSystem.Received(1).FinalizeSpyReturn(PlayerColor.Blue);
        }
    }
}
