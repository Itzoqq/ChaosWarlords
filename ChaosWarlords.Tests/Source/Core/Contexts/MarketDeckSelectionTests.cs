using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Contexts
{
    [TestClass]
    [TestCategory("Unit")]
    public class MarketDeckSelectionTests
    {
        [TestMethod]
        public void Constructor_WithDuplicateHalfDecks_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Drow));
        }

        [TestMethod]
        public void NextDistinct_SkipsTheOtherSelectedHalfDeck()
        {
            var next = MarketDeckSelection.NextDistinct(MarketHalfDeck.Drow, MarketHalfDeck.Dragons);

            Assert.AreEqual(MarketHalfDeck.Elementals, next);
        }

        [TestMethod]
        public void WithFirst_WhenGivenTheOtherSelectedHalfDeck_Throws()
        {
            var selection = new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Dragons);

            Assert.ThrowsExactly<ArgumentException>(() => selection.WithFirst(MarketHalfDeck.Dragons));
        }

        [TestMethod]
        public void Default_UsesTheRulebookRecommendedDrowAndDragonDecks()
        {
            Assert.IsTrue(MarketDeckSelection.Default.Includes(MarketHalfDeck.Drow));
            Assert.IsTrue(MarketDeckSelection.Default.Includes(MarketHalfDeck.Dragons));
            Assert.IsFalse(MarketDeckSelection.Default.Includes(MarketHalfDeck.Demons));
        }
    }
}
