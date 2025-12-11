using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

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
            // Using a dummy constructor for Card as it's not in context, similar to other test files.
            _card1 = new Card("c1", "c1", 0, CardAspect.Shadow, 0, 0);
            _card2 = new Card("c2", "c2", 0, CardAspect.Shadow, 0, 0);
            _card3 = new Card("c3", "c3", 0, CardAspect.Shadow, 0, 0);
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
    }
}