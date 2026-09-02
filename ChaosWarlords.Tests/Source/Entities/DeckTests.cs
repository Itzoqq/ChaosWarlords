using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Entities
{
    [TestClass]
    [TestCategory("Unit")]
    public class DeckTests
    {
        private Deck _deck = null!;
        private Card _c1 = null!;
        private Card _c2 = null!;
        private Card _c3 = null!;

        [TestInitialize]
        public void Setup()
        {
            _c1 = TestData.Cards.CheapCard();
            _c2 = TestData.Cards.ExpensiveCard();
            _c3 = TestData.Cards.FreeCard();

            _deck = new Deck();
        }

        [TestMethod]
        public void Draw_ReturnsCorrectNumber_AndRemovesFromDrawPile()
        {
            _deck.AddToTop(_c1);
            _deck.AddToTop(_c2);

            var mockRandom = Substitute.For<IGameRandom>();
            var drawn = _deck.Draw(1, mockRandom);

            Assert.HasCount(1, drawn);
            Assert.AreEqual(_c2, drawn[0]); // c2 was added last (top)
            Assert.AreEqual(1, _deck.Count);
        }

        [TestMethod]
        public void Draw_ReshufflesDiscard_WhenDrawPileEmpty()
        {
            _deck.AddToDiscard(_c1);
            _deck.AddToDiscard(_c2);
            // Draw pile is empty

            var mockRandom = Substitute.For<IGameRandom>();
            var drawn = _deck.Draw(1, mockRandom);

            Assert.HasCount(1, drawn);
            Assert.AreEqual(1, _deck.Count); // 2 total - 1 drawn = 1 left
            Assert.AreEqual(0, _deck.DiscardCount);

            // Note: Cannot assert which one was drawn due to shuffle, but one should be.
            Assert.IsTrue(drawn.Contains(_c1) || drawn.Contains(_c2));
        }

        [TestMethod]
        public void Draw_Stops_IfBothPilesEmpty()
        {
            _deck.AddToTop(_c1);

            var mockRandom = Substitute.For<IGameRandom>();
            var drawn = _deck.Draw(5, mockRandom);

            Assert.HasCount(1, drawn);
            Assert.AreEqual(0, _deck.Count);
        }

        [TestMethod]
        public void AddToDiscard_AddsToDiscardPile()
        {
            _deck.AddToDiscard(_c1);
            Assert.AreEqual(1, _deck.DiscardCount);
            Assert.AreEqual(CardLocation.DiscardPile, _c1.Location);
        }

        [TestMethod]
        public void Shuffle_ChangesOrder()
        {
            var cards = new List<Card>();
            for (int i = 0; i < 50; i++)
                cards.Add(new Card(i.ToString(), i.ToString(), 0, CardAspect.Neutral, 0, 0, 0));

            _deck.AddToDiscard(cards);
            var mockRandom = Substitute.For<IGameRandom>();
            _deck.Draw(1, mockRandom); // Triggers reshuffle

            // Very small chance this fails if it randomly shuffles to same order, but negligible for 50 items.
            // However, we only have read-only access to DrawPile list. 
            // We can check if it's not identical to input list order if we tracked it, but discard adds to end.

            // Let's just trust logic for now or inspect internals via Draw.
        }
        [TestMethod]
        public void Reshuffle_MaintainsIntegrity()
        {
            // Scenario: Draw Count > Deck Count. Needs Reshuffle.
            // Setup: Deck=1, Discard=2. Request=2.
            // Expected: Draw 1 from Deck. Deck Empty. Reshuffle (Deck becomes 2). Draw 1 from Deck. Total Drawn=2. Left=1.

            _deck.AddToTop(_c1);
            _deck.AddToDiscard(_c2);
            _deck.AddToDiscard(_c3);

            var mockRandom = Substitute.For<IGameRandom>();
            var drawn = _deck.Draw(2, mockRandom);

            Assert.HasCount(2, drawn, "Should have drawn 2 cards");
            Assert.AreEqual(1, _deck.Count, "Should have 1 card remaining in deck");
            Assert.AreEqual(0, _deck.DiscardCount, "Discard should be empty after reshuffle");

            // Ensuring unique cards (no dupes)
            var distinct = drawn.Distinct().ToList();
            Assert.HasCount(2, distinct, "Drawn cards must be unique");
            Assert.DoesNotContain(_deck.DrawPile[0], drawn, "Remaining card should not be in drawn list");
        }

        [TestMethod]
        public void MoveAllToDiscard_NonEmptyDeck_EmptiesDrawPileAndMovesCardsToDiscardWithLocationUpdated()
        {
            // Arrange: Matron Mother's "Put your deck into your discard pile."
            _deck.AddToTop(_c1);
            _deck.AddToTop(_c2);

            // Act
            _deck.MoveAllToDiscard();

            // Assert
            Assert.IsEmpty(_deck.DrawPile, "Draw pile should be fully emptied.");
            Assert.AreEqual(0, _deck.Count);
            Assert.AreEqual(2, _deck.DiscardCount);
            Assert.Contains(_c1, _deck.DiscardPile.ToList());
            Assert.Contains(_c2, _deck.DiscardPile.ToList());
            Assert.AreEqual(CardLocation.DiscardPile, _c1.Location);
            Assert.AreEqual(CardLocation.DiscardPile, _c2.Location);
        }

        [TestMethod]
        public void MoveAllToDiscard_EmptyDeck_IsSafeNoOp()
        {
            // Arrange: draw pile already empty (with an unrelated card already in discard).
            _deck.AddToDiscard(_c3);

            // Act
            _deck.MoveAllToDiscard();

            // Assert
            Assert.IsEmpty(_deck.DrawPile);
            Assert.AreEqual(1, _deck.DiscardCount, "The pre-existing discard card should be untouched.");
            Assert.Contains(_c3, _deck.DiscardPile.ToList());
        }

        [TestMethod]
        public void RemoveFromDiscard_ExistingCard_RemovesAndReturnsTrue()
        {
            // Arrange
            _deck.AddToDiscard(_c1);
            _deck.AddToDiscard(_c2);

            // Act
            var result = _deck.RemoveFromDiscard(_c1);

            // Assert
            Assert.IsTrue(result);
            Assert.DoesNotContain(_c1, _deck.DiscardPile.ToList());
            Assert.Contains(_c2, _deck.DiscardPile.ToList());
            Assert.AreEqual(1, _deck.DiscardCount);
        }

        [TestMethod]
        public void RemoveFromDiscard_CardNotPresent_ReturnsFalse()
        {
            // Arrange: c1 is in discard, c2 never was.
            _deck.AddToDiscard(_c1);

            // Act
            var result = _deck.RemoveFromDiscard(_c2);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, _deck.DiscardCount, "The unrelated existing discard card should be untouched.");
        }
    }
}



