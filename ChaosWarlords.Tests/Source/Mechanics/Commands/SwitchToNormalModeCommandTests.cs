using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class SwitchToNormalModeCommandTests
    {
        [TestMethod]
        public void Execute_SwitchesToNormalMode()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var command = new SwitchToNormalModeCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).SwitchToNormalMode();
        }
    }
}


