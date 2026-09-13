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
        public void StartReplay_WithUnparseableHalfDeckSelection_FallsBackToDefault()
        {
            var manager = new ReplayManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            string replay = System.Text.Json.JsonSerializer.Serialize(new ReplayDataDto
            {
                FirstMarketHalfDeck = "NotARealHalfDeck",
                SecondMarketHalfDeck = "Dragons"
            });

            manager.StartReplay(replay);

            Assert.AreEqual(MarketHalfDeck.Drow, manager.MarketDeckSelection.First);
            Assert.AreEqual(MarketHalfDeck.Dragons, manager.MarketDeckSelection.Second);
        }

        [TestMethod]
        public void StartReplay_WithAPreMigrationAspectNameRecordedInTheHalfDeckField_FallsBackToDefault()
        {
            // An older replay file recorded before TIER 1 item 5 (when this same field held a
            // CardAspect name, e.g. "Warlord"/"Sorcery") is exactly this shape once reloaded
            // against the current MarketHalfDeck-based parser - neither string parses as a
            // MarketHalfDeck, so this must fail closed to Default rather than throw.
            var manager = new ReplayManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            string replay = System.Text.Json.JsonSerializer.Serialize(new ReplayDataDto
            {
                FirstMarketHalfDeck = "Warlord",
                SecondMarketHalfDeck = "Sorcery"
            });

            manager.StartReplay(replay);

            Assert.AreEqual(MarketHalfDeck.Drow, manager.MarketDeckSelection.First);
            Assert.AreEqual(MarketHalfDeck.Dragons, manager.MarketDeckSelection.Second);
        }
    }
}
