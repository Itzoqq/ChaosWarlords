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

            Assert.AreEqual(MarketHalfDeck.Elemental, next);
        }

        [TestMethod]
        public void NextDistinct_WrapsAroundToTheFirstEnumValue()
        {
            // Undead is the last declared MarketHalfDeck value - wrapping must land back on Drow
            // (the first), not throw or stay stuck.
            var next = MarketDeckSelection.NextDistinct(MarketHalfDeck.Undead, MarketHalfDeck.Aberrations);

            Assert.AreEqual(MarketHalfDeck.Drow, next);
        }

        [TestMethod]
        public void NextDistinct_WithACandidateList_OnlyCyclesThroughThoseCandidates()
        {
            var candidates = new[] { MarketHalfDeck.Drow, MarketHalfDeck.Dragons };

            var next = MarketDeckSelection.NextDistinct(MarketHalfDeck.Drow, MarketHalfDeck.Dragons, candidates);

            Assert.AreEqual(MarketHalfDeck.Drow, next, "With only 2 candidates and the other one excluded, there's nothing else to cycle to.");
        }

        [TestMethod]
        public void NextDistinct_WithACandidateListNotContainingCurrent_StartsScanningFromTheTop()
        {
            var candidates = new[] { MarketHalfDeck.Drow, MarketHalfDeck.Dragons };

            // `current` (Elemental) just fell out of the candidate list (e.g. it stopped being
            // complete) - cycling must still land on a real candidate, not throw or hang.
            var next = MarketDeckSelection.NextDistinct(MarketHalfDeck.Elemental, MarketHalfDeck.Dragons, candidates);

            Assert.AreEqual(MarketHalfDeck.Drow, next);
        }

        [TestMethod]
        public void NextDistinct_WithAnEmptyCandidateList_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => MarketDeckSelection.NextDistinct(MarketHalfDeck.Drow, MarketHalfDeck.Dragons, []));
        }

        [TestMethod]
        public void WithFirst_WhenGivenTheOtherSelectedHalfDeck_Throws()
        {
            var selection = new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Dragons);

            Assert.ThrowsExactly<ArgumentException>(() => selection.WithFirst(MarketHalfDeck.Dragons));
        }

        [TestMethod]
        public void WithSecond_ReplacesOnlyTheSecondHalfDeck()
        {
            var selection = new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Dragons);

            var updated = selection.WithSecond(MarketHalfDeck.Demons);

            Assert.AreEqual(MarketHalfDeck.Drow, updated.First);
            Assert.AreEqual(MarketHalfDeck.Demons, updated.Second);
        }

        [TestMethod]
        public void Default_UsesDrowAndDragonsHalfDecks()
        {
            // Rulebook p.4: "First Game. For your first game use the Drow and Dragon half-decks."
            Assert.IsTrue(MarketDeckSelection.Default.Includes(MarketHalfDeck.Drow));
            Assert.IsTrue(MarketDeckSelection.Default.Includes(MarketHalfDeck.Dragons));
            Assert.IsFalse(MarketDeckSelection.Default.Includes(MarketHalfDeck.Elemental));
        }
    }
}
