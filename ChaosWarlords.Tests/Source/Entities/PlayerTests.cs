using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Entities
{
    [TestClass]
    public class PlayerTests
    {
        private Player _player = null!;
        private Card _card1 = null!, _card2 = null!, _card3 = null!;

        [TestInitialize]
        public void Setup()
        {
            _player = new Player(PlayerColor.Red);
            _card1 = new Card("c1", "Card One", 0, CardAspect.Shadow, 1, 2);
            _card2 = new Card("c2", "Card Two", 0, CardAspect.Shadow, 1, 2);
            _card3 = new Card("c3", "Card Three", 0, CardAspect.Shadow, 1, 2);
        }

        [TestMethod]
        public void DrawCards_DrawsCorrectAmountFromDeck()
        {
            _player.Deck.AddRange(new[] { _card1, _card2, _card3 });

            _player.DrawCards(2);

            Assert.HasCount(2, _player.Hand);
            Assert.HasCount(1, _player.Deck);
            Assert.AreEqual(CardLocation.Hand, _player.Hand[0].Location);
        }

        [TestMethod]
        public void DrawCards_ReshufflesDiscardPileWhenDeckIsEmpty()
        {
            _player.Deck.Add(_card1);
            _player.DiscardPile.AddRange(new[] { _card2, _card3 });

            _player.DrawCards(3);

            Assert.HasCount(3, _player.Hand);
            Assert.IsEmpty(_player.Deck);
            Assert.IsEmpty(_player.DiscardPile);
            CollectionAssert.Contains(_player.Hand, _card1);
            CollectionAssert.Contains(_player.Hand, _card2);
            CollectionAssert.Contains(_player.Hand, _card3);
        }

        [TestMethod]
        public void DrawCards_StopsWhenDeckAndDiscardAreEmpty()
        {
            _player.Deck.Add(_card1);

            _player.DrawCards(5); // Tries to draw 5, but only 1 is available

            Assert.HasCount(1, _player.Hand);
            Assert.IsEmpty(_player.Deck);
        }

        [TestMethod]
        public void CleanUpTurn_MovesCardsToDiscardAndResetsResources()
        {
            _player.Hand.AddRange(new[] { _card1, _card2 });
            _player.PlayedCards.Add(_card3);
            _player.Power = 10;
            _player.Influence = 5;

            _player.CleanUpTurn();

            Assert.IsEmpty(_player.Hand);
            Assert.IsEmpty(_player.PlayedCards);
            Assert.HasCount(3, _player.DiscardPile);
            Assert.AreEqual(0, _player.Power);
            Assert.AreEqual(0, _player.Influence);
            CollectionAssert.Contains(_player.DiscardPile, _card1);
            CollectionAssert.Contains(_player.DiscardPile, _card2);
            CollectionAssert.Contains(_player.DiscardPile, _card3);
        }

        [TestMethod]
        public void PromoteCard_MovesCardToInnerCircle()
        {
            // Arrange
            _player.Hand.Add(_card1);
            Assert.AreEqual(CardLocation.None, _card1.Location); // Or whatever default is

            // Act
            _player.PromoteCard(_card1);

            // Assert
            Assert.Contains(_card1, _player.InnerCircle, "Card should be in Inner Circle list");
            Assert.DoesNotContain(_card1, _player.Hand, "Card should be removed from Hand");
            Assert.AreEqual(CardLocation.InnerCircle, _card1.Location);
        }

        [TestMethod]
        public void PromoteCard_PreventsCardFromGoingToDiscard_OnCleanup()
        {
            // Arrange
            _player.Hand.Add(_card1); // Will be promoted
            _player.Hand.Add(_card2); // Will be discarded (unused)
            _player.PlayedCards.Add(_card3); // Will be discarded (played)

            // Act
            _player.PromoteCard(_card1); // Action happens
            _player.CleanUpTurn();       // Turn ends

            // Assert
            // 1. Inner Circle check
            Assert.HasCount(1, _player.InnerCircle);
            Assert.AreEqual(_card1, _player.InnerCircle[0]);

            // 2. Discard Pile check
            Assert.HasCount(2, _player.DiscardPile);
            CollectionAssert.Contains(_player.DiscardPile, _card2);
            CollectionAssert.Contains(_player.DiscardPile, _card3);

            // 3. Crucial Check: Promoted card is NOT in discard
            CollectionAssert.DoesNotContain(_player.DiscardPile, _card1, "Promoted card should not enter discard cycle.");
        }

        [TestMethod]
        public void Resources_CannotGoNegative()
        {
            _player.Power = 5;
            _player.Power -= 10; // Should be clamped

            Assert.AreEqual(0, _player.Power, "Power should clamp to 0.");

            _player.Influence = 0;
            _player.Influence = -1;

            Assert.AreEqual(0, _player.Influence, "Influence should clamp to 0.");
        }
    }
}