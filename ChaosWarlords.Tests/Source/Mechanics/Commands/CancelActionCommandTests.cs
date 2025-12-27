using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class CancelActionCommandTests
    {
        [TestMethod]
        public void Execute_CancelsTargetingAndSwitchesToNormalMode()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockState.ActionSystem.Returns(mockActionSystem);
            var command = new CancelActionCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockActionSystem.Received(1).CancelTargeting();
            mockState.Received(1).SwitchToNormalMode();
        }
    }
}
