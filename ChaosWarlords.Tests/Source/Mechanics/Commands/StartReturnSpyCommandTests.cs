using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class StartReturnSpyCommandTests
    {
        [TestMethod]
        public void Execute_StartsReturnSpyAndSwitchesToTargetingMode()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.TargetingReturnSpy);
            mockState.ActionSystem.Returns(mockActionSystem);
            var command = new StartReturnSpyCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockActionSystem.Received(1).TryStartReturnSpy();
            mockState.Received(1).SwitchToTargetingMode();
        }
    }
}


