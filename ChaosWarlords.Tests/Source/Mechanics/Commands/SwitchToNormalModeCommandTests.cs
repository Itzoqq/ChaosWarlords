using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class SwitchToNormalModeCommandTests
    {
        [TestMethod]
        public void Execute_SwitchesToNormalMode()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            stateFake.ActiveModeName = "Targeting"; // Start in a different mode
            
            var command = new SwitchToNormalModeCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            Assert.AreEqual("Normal", stateFake.ActiveModeName, "Should have switched to Normal mode.");
        }
    }
}
