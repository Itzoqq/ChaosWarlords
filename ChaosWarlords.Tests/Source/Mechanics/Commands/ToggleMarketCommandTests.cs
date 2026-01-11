using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class ToggleMarketCommandTests
    {
        [TestMethod]
        public void Execute_WhenMarketClosed_OpenMarket()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            // MarketManager in logic also implements IMarketStateManager
            var mockState = (IMarketStateManager)stateFake.MarketManager;
            mockState.IsOpen.Returns(false);
            
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            mockState.Received(1).OpenForBrowsing();
        }

        [TestMethod]
        public void Execute_WhenMarketOpen_ClosesMarket()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            // MarketManager in logic also implements IMarketStateManager
            var mockState = (IMarketStateManager)stateFake.MarketManager;
            mockState.IsOpen.Returns(true);
            
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            mockState.Received(1).Close();
        }
    }
}
