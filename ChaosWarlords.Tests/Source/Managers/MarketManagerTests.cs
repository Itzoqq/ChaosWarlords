using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class MarketManagerTests
    {
        private MarketManager _market = null!;
        private Player _player = null!;
        private Card _cheapCard = null!;
        private Card _expensiveCard = null!;
        private ICardDatabase _mockDb = null!;

        [TestInitialize]
        public void Setup()
        {
            _player = new Player(PlayerColor.Red);

            // Setup dummy cards
            _cheapCard = new Card("c1", "Cheap", 2, CardAspect.Neutral, 1, 1, 0);
            _expensiveCard = new Card("c2", "Expensive", 10, CardAspect.Neutral, 1, 1, 0);

            // 1. Create the Mock using NSubstitute
            _mockDb = Substitute.For<ICardDatabase>();

            // 2. Configure the Mock behavior
            // We return a list containing our test cards.
            // Note: MarketManager modifies this list, so we create a fresh list in Setup() every time.
            var deck = new List<Card> { _cheapCard, _expensiveCard };
            _mockDb.GetAllMarketCards().Returns(deck);

            // 3. Inject the Mock
            _market = new MarketManager(_mockDb);
        }

        [TestMethod]
        public void TryBuyCard_Succeeds_WhenAffordable()
        {
            _player.Influence = 5;

            // The cheap card is in the deck provided by the mock in Setup()
            // Note: Deck is shuffled, so we must find the card instance in the row if it exists
            // Or assume setup creates small deck where both might be in row (deck size 2 < row size 6)
            bool result = _market.TryBuyCard(_player, _cheapCard);

            Assert.IsTrue(result);
            Assert.AreEqual(3, _player.Influence);
            Assert.IsTrue(_player.DiscardPile.Contains(_cheapCard));
            CollectionAssert.DoesNotContain(_market.MarketRow, _cheapCard);
        }

        [TestMethod]
        public void TryBuyCard_Fails_WhenTooExpensive()
        {
            _player.Influence = 5;

            bool result = _market.TryBuyCard(_player, _expensiveCard);

            Assert.IsFalse(result);
            Assert.AreEqual(5, _player.Influence);
            Assert.IsFalse(_player.DiscardPile.Contains(_expensiveCard));
        }

        [TestMethod]
        public void RefillMarket_Refills_WhenCardPurchased_AndDeckHasReserves()
        {
            // Arrange: Create a specific scenario with more cards than the default Setup
            var cards = new List<Card>();
            for (int i = 0; i < 6; i++)
            {
                cards.Add(new Card($"fill{i}", "Filler", 1, CardAspect.Neutral, 0, 0, 0));
            }
            var reserveCard = new Card("reserve", "Reserve", 1, CardAspect.Neutral, 0, 0, 0);
            cards.Add(reserveCard);

            // Create a local mock for this specific test
            var localMockDb = Substitute.For<ICardDatabase>();
            // Return a COPY so we can compare against original list later
            localMockDb.GetAllMarketCards().Returns(new List<Card>(cards));

            var localMarket = new MarketManager(localMockDb);

            _player.Influence = 100;

            // Handle Random Shuffle
            // Identify which card was left in the deck
            var cardsInRow = localMarket.MarketRow;
            var cardLeftInDeck = cards.Except(cardsInRow).FirstOrDefault();

            Assert.IsNotNull(cardLeftInDeck, "Should be 1 card left in the deck (7 total, 6 in row).");
            Assert.HasCount(6, localMarket.MarketRow);

            // Act: Buy one card to trigger refill
            var cardToBuy = localMarket.MarketRow[0];
            bool bought = localMarket.TryBuyCard(_player, cardToBuy);

            // Assert
            Assert.IsTrue(bought);
            Assert.HasCount(6, localMarket.MarketRow, "Market should refill back to 6");
            CollectionAssert.Contains(localMarket.MarketRow, cardLeftInDeck, "The specific card from the deck should have entered the row.");
        }

        [TestMethod]
        public void TryBuyCard_Succeeds_WithExactFunds()
        {
            // Arrange
            var exactCard = new Card("c_exact", "Exact", 3, CardAspect.Neutral, 1, 0, 0);

            // Manually add to the row (bypassing the deck/mock for this specific setup)
            _market.MarketRow.Add(exactCard);
            _player.Influence = 3;

            // Act
            bool result = _market.TryBuyCard(_player, exactCard);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _player.Influence);
            Assert.IsTrue(_player.DiscardPile.Contains(exactCard));
        }

        [TestMethod]
        public void TryBuyCard_Fails_WithZeroFunds()
        {
            _player.Influence = 0;

            bool result = _market.TryBuyCard(_player, _cheapCard);

            Assert.IsFalse(result);
            Assert.AreEqual(0, _player.Influence);
            Assert.IsFalse(_player.DiscardPile.Contains(_cheapCard));
        }
    }
}


