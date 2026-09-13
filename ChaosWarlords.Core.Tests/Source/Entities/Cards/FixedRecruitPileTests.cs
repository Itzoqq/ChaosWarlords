using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Core.Tests.Source.Entities.Cards
{
    [TestClass]
    [TestCategory("Unit")]
    public class FixedRecruitPileTests
    {
        [TestMethod]
        public void Constructor_EmptyPileWithDisplayName_RetainsTheNameForItsEmptyPileMarker()
        {
            var pile = new FixedRecruitPile("core_priestess", [], "Priestess of Light");

            Assert.AreEqual("Priestess of Light", pile.DisplayName);
            Assert.IsNull(pile.AvailableCard);
        }
    }
}
