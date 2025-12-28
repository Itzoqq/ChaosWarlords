using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using NSubstitute;

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
            _card1 = new Card("c1", "Card One", 0, CardAspect.Shadow, 1, 2, 0);
            _card2 = new Card("c2", "Card Two", 0, CardAspect.Shadow, 1, 2, 0);
            _card3 = new Card("c3", "Card Three", 0, CardAspect.Shadow, 1, 2, 0);
        }

        [TestMethod]
        public void DrawCards_DrawsCorrectAmountFromDeck()
        {
            _player.DeckManager.AddToTop(_card1);
            _player.DeckManager.AddToTop(_card2);
            _player.DeckManager.AddToTop(_card3);

            var mockRandom = Substitute.For<IGameRandom>();
            _player.DrawCards(2, mockRandom);

            Assert.HasCount(2, _player.Hand);
            Assert.HasCount(1, _player.Deck);
            Assert.AreEqual(CardLocation.Hand, _player.Hand[0].Location);
        }

        [TestMethod]
        public void DrawCards_ReshufflesDiscardPileWhenDeckIsEmpty()
        {
            _player.DeckManager.AddToTop(_card1);
            _player.DeckManager.AddToDiscard(new[] { _card2, _card3 });

            var mockRandom = Substitute.For<IGameRandom>();
            _player.DrawCards(3, mockRandom);

            Assert.HasCount(3, _player.Hand);
            Assert.IsEmpty(_player.Deck);
            Assert.IsEmpty(_player.DiscardPile);

            // Since order is shuffled, just check containment
            CollectionAssert.Contains(_player.Hand, _card1);
            CollectionAssert.Contains(_player.Hand, _card2);
            CollectionAssert.Contains(_player.Hand, _card3);
        }

        [TestMethod]
        public void DrawCards_StopsWhenDeckAndDiscardAreEmpty()
        {
            _player.DeckManager.AddToTop(_card1);

            var mockRandom = Substitute.For<IGameRandom>();
            _player.DrawCards(5, mockRandom); // Tries to draw 5, but only 1 is available

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
            // We cannot easily check containment on IReadOnlyList with CollectionAssert directly if it expects ICollection? 
            // CollectionAssert works on ICollection. IReadOnlyList implements IEnumerable, usually generic. 
            // CollectionAssert.Contains expects ICollection or ICollection via explicit check. 
            // Let's rely on Linq or check behavior.
            Assert.IsTrue(_player.DiscardPile.Contains(_card1));
            Assert.IsTrue(_player.DiscardPile.Contains(_card2));
            Assert.IsTrue(_player.DiscardPile.Contains(_card3));
        }

        [TestMethod]
        public void PromoteCard_MovesCardToInnerCircle()
        {
            // Arrange
            _player.Hand.Add(_card1);
            Assert.AreEqual(CardLocation.None, _card1.Location); // Or whatever default is

            // Act
            bool success = _player.TryPromoteCard(_card1, out string errorMessage);

            // Assert
            Assert.IsTrue(success, "Promotion should succeed");
            Assert.AreEqual(string.Empty, errorMessage);
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
            bool success = _player.TryPromoteCard(_card1, out string errorMessage); // Action happens
            _player.CleanUpTurn();       // Turn ends

            // Assert
            Assert.IsTrue(success, "Promotion should succeed");
            // 1. Inner Circle check
            Assert.HasCount(1, _player.InnerCircle);
            Assert.AreEqual(_card1, _player.InnerCircle[0]);

            // 2. Discard Pile check
            Assert.HasCount(2, _player.DiscardPile);
            Assert.IsTrue(_player.DiscardPile.Contains(_card2));
            Assert.IsTrue(_player.DiscardPile.Contains(_card3));

            // 3. Crucial Check: Promoted card is NOT in discard
            Assert.IsFalse(_player.DiscardPile.Contains(_card1), "Promoted card should not enter discard cycle.");
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
        [TestMethod]
        public void CleanUpTurn_EnsuresListsAreEmpty()
        {
            // Verify strict clearing of lists to prevent "sticky cards"
            _player.Hand.Add(_card1);
            _player.PlayedCards.Add(_card2);

            _player.CleanUpTurn();

            Assert.IsEmpty(_player.Hand, "Hand must be empty after cleanup");
            Assert.IsEmpty(_player.PlayedCards, "PlayedCards must be empty after cleanup");
        }

        [TestMethod]
        public void ConservationOfMass_Simulation()
        {
            // Simulate 100 turns to ensure no cards are lost (e.g. drawn but not added to hand, or lost during reshuffle)
            // Goal: Total Cards (Deck + Hand + Discard + Played) must always equal Initial Count.

            // Setup: 10 Cards
            int totalCards = 10;
            for (int i = 0; i < totalCards; i++)
            {
                _player.DeckManager.AddToTop(new Card($"sim_{i}", "Sim", 0, CardAspect.Neutral, 0, 0, 0));
            }

            int handSize = 5;

            for (int turn = 1; turn <= 100; turn++)
            {
                // 1. Draw
                var mockRandom = Substitute.For<IGameRandom>();
                _player.DrawCards(handSize, mockRandom);

                // Verify Hand Size (unless deck total < 5, which shouldn't happen here)
                Assert.HasCount(handSize, _player.Hand, $"Turn {turn}: Hand size incorrect.");

                // Check Mass
                int currentTotal = _player.Deck.Count + _player.Hand.Count + _player.DiscardPile.Count + _player.PlayedCards.Count;
                Assert.AreEqual(totalCards, currentTotal, $"Turn {turn}: Conservation of mass violated after Draw.");

                // 2. Play some cards (Move Hand -> Played)
                int playCount = 3;
                for (int j = 0; j < playCount; j++)
                {
                    var card = _player.Hand[0];
                    _player.Hand.RemoveAt(0);
                    _player.PlayedCards.Add(card);
                }

                // Check Mass
                currentTotal = _player.Deck.Count + _player.Hand.Count + _player.DiscardPile.Count + _player.PlayedCards.Count;
                Assert.AreEqual(totalCards, currentTotal, $"Turn {turn}: Conservation of mass violated after Play.");

                // 3. Cleanup
                _player.CleanUpTurn();

                // Check Mass
                currentTotal = _player.Deck.Count + _player.Hand.Count + _player.DiscardPile.Count + _player.PlayedCards.Count;
                Assert.AreEqual(totalCards, currentTotal, $"Turn {turn}: Conservation of mass violated after Cleanup.");
            }
        }
    }
}


