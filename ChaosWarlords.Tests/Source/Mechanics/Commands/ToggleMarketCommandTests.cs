using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

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
            stateFake.MarketStateManager.Close();
            
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            Assert.IsTrue(stateFake.IsMarketOpen, "Market should be open.");
        }

        [TestMethod]
        public void Execute_WhenMarketOpen_ClosesMarket()
        {
            // Arrange
            var stateFake = new TestGameplayState();
            stateFake.MarketStateManager.OpenForBrowsing();
            
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(stateFake);

            // Assert
            Assert.IsFalse(stateFake.IsMarketOpen, "Market should be closed.");
        }
    }
}
