using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Contexts
{
    [TestClass]
    [TestCategory("Unit")]
    public class MarketDeckSelectionTests
    {
        [TestMethod]
        public void Constructor_WithDuplicateAspects_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new MarketDeckSelection(CardAspect.Warlord, CardAspect.Warlord));
        }

        [TestMethod]
        public void Constructor_WithNonMarketAspect_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new MarketDeckSelection(CardAspect.Neutral, CardAspect.Warlord));
        }

        [TestMethod]
        public void NextDistinct_SkipsTheOtherSelectedAspect()
        {
            var next = MarketDeckSelection.NextDistinct(CardAspect.Warlord, CardAspect.Sorcery);

            Assert.AreEqual(CardAspect.Shadow, next);
        }

        [TestMethod]
        public void WithFirst_WhenGivenTheOtherSelectedAspect_Throws()
        {
            var selection = new MarketDeckSelection(CardAspect.Warlord, CardAspect.Sorcery);

            Assert.ThrowsExactly<ArgumentException>(() => selection.WithFirst(CardAspect.Sorcery));
        }

        [TestMethod]
        public void Default_UsesWarlordAndSorceryAspects()
        {
            Assert.IsTrue(MarketDeckSelection.Default.Includes(CardAspect.Warlord));
            Assert.IsTrue(MarketDeckSelection.Default.Includes(CardAspect.Sorcery));
            Assert.IsFalse(MarketDeckSelection.Default.Includes(CardAspect.Shadow));
        }
    }
}
