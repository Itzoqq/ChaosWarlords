using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class ReplayManagerAspectSelectionTests
    {
        [TestMethod]
        public void StartReplay_WithNonMarketAspectSelection_FallsBackToDefault()
        {
            var manager = new ReplayManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            string replay = System.Text.Json.JsonSerializer.Serialize(new ReplayDataDto
            {
                FirstMarketAspect = "Neutral",
                SecondMarketAspect = "Warlord"
            });

            manager.StartReplay(replay);

            Assert.AreEqual(CardAspect.Warlord, manager.MarketDeckSelection.First);
            Assert.AreEqual(CardAspect.Sorcery, manager.MarketDeckSelection.Second);
        }
    }
}
