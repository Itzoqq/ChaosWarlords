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

        // Regression coverage for planning.txt TIER 1's spam/idempotency audit (section 6.C.3):
        // ToggleMarketCommand can't be exercised through the real MatchScenario harness (the
        // real MarketManager doesn't implement IMarketStateManager - that's a client-only
        // concern, mocked here), so its "dispatched twice back-to-back" case belongs at this
        // unit level instead.
        [TestMethod]
        public void Execute_DispatchedTwiceBackToBack_TogglesBothTimes()
        {
            var stateFake = new TestGameplayState();
            var mockState = (IMarketStateManager)stateFake.MarketManager;
            mockState.IsOpen.Returns(false);

            var command = new ToggleMarketCommand();

            command.Execute(stateFake.MatchContext);
            command.Execute(stateFake.MatchContext);

            // Same command instance, same stubbed IsOpen (the stub doesn't track state changes
            // by itself) - both dispatches should independently call the correct method for
            // whatever IsOpen reports at that moment, not skip/double-fire on the second call.
            mockState.Received(2).OpenForBrowsing();
        }
    }
}
