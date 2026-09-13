using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Rendering.UI
{
    [TestClass]
    [TestCategory("Unit")]
    public class MarketLayoutTests
    {
        [TestMethod]
        public void GetMarketRowPosition_AdjacentCards_UsesTheConfiguredMarketGap()
        {
            var first = MarketLayout.GetMarketRowPosition(0);
            var second = MarketLayout.GetMarketRowPosition(1);

            Assert.AreEqual(GameConstants.CardRendering.MarketStartY, first.Y);
            Assert.AreEqual(Card.Width + GameConstants.CardRendering.MarketCardGap, second.X - first.X);
        }

        [TestMethod]
        public void GetFixedRecruitPilePosition_TwoPiles_PlacesThemBelowAndAwayFromMarketRow()
        {
            var first = MarketLayout.GetFixedRecruitPilePosition(0, 2, 1280);
            var second = MarketLayout.GetFixedRecruitPilePosition(1, 2, 1280);

            Assert.AreEqual(GameConstants.CardRendering.MarketStartY + Card.Height + MarketLayout.FixedPileTopMargin, first.Y);
            Assert.AreEqual(first.Y, second.Y);
            Assert.AreEqual(Card.Width + MarketLayout.FixedPileGap, second.X - first.X);
            Assert.IsGreaterThan(GameConstants.CardRendering.MarketStartY + Card.Height, first.Y);
        }

        [TestMethod]
        public void GetFixedRecruitPilePosition_ViewportTooNarrow_Throws()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MarketLayout.GetFixedRecruitPilePosition(0, 2, Card.Width - 1));
        }

        [TestMethod]
        public void GetFixedRecruitPilePosition_IndexOutsidePileCount_Throws()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MarketLayout.GetFixedRecruitPilePosition(2, 2, 1280));
        }
    }
}
