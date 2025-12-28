using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class ToggleMarketCommandTests
    {
        [TestMethod]
        public void Execute_WhenMarketClosed_OpensMarket()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            mockState.IsMarketOpen.Returns(false);
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).ToggleMarket();
        }

        [TestMethod]
        public void Execute_WhenMarketOpen_ClosesMarket()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            mockState.IsMarketOpen.Returns(true);
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).CloseMarket();
        }
    }
}


