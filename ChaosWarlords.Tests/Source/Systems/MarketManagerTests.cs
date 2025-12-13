using ChaosWarlords.Source.Entities;
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

        [TestInitialize]
        public void Setup()
        {
            _market = new MarketManager();
            _player = new Player(PlayerColor.Red);

            // Setup dummy cards
            _cheapCard = new Card("c1", "Cheap", 2, CardAspect.Neutral, 1, 1);
            _expensiveCard = new Card("c2", "Expensive", 10, CardAspect.Neutral, 1, 1);

            // Initialize market with these cards
            var deck = new List<Card> { _cheapCard, _expensiveCard };
            _market.InitializeDeck(deck);
        }

        [TestMethod]
        public void TryBuyCard_Succeeds_WhenAffordable()
        {
            _player.Influence = 5;

            bool result = _market.TryBuyCard(_player, _cheapCard);

            Assert.IsTrue(result);
            Assert.AreEqual(3, _player.Influence); // 5 - 2
            Assert.Contains(_cheapCard, _player.DiscardPile);
            Assert.DoesNotContain(_cheapCard, _market.MarketRow);
        }

        [TestMethod]
        public void TryBuyCard_Fails_WhenTooExpensive()
        {
            _player.Influence = 5;

            bool result = _market.TryBuyCard(_player, _expensiveCard);

            Assert.IsFalse(result);
            Assert.AreEqual(5, _player.Influence);
            Assert.DoesNotContain(_expensiveCard, _player.DiscardPile);
        }

        [TestMethod]
        public void RefillMarket_FillsEmptySlots()
        {
            // Market should start full (or max available)
            // In setup we only gave 2 cards, so row has 2.
            Assert.HasCount(2, _market.MarketRow);

            // Buy one
            _player.Influence = 100;
            _market.TryBuyCard(_player, _cheapCard);

            // Since deck was empty after initial fill, it won't refill from deck
            // but the method logic should hold. 
            // Let's add more to deck to test refill.
            var extraCard = new Card("c3", "Extra", 1, CardAspect.Neutral, 0, 0);
            _market.MarketDeck.Add(extraCard);

            _market.RefillMarket();

            Assert.Contains(extraCard, _market.MarketRow);
        }

        [TestMethod]
        public void TryBuyCard_Succeeds_WithExactFunds()
        {
            // Arrange
            _cheapCard = new Card("c1", "Exact", 3, CardAspect.Neutral, 1, 0);
            _market.MarketRow.Add(_cheapCard);
            _player.Influence = 3;

            // Act
            bool result = _market.TryBuyCard(_player, _cheapCard);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _player.Influence);
            Assert.Contains(_cheapCard, _player.DiscardPile);
        }

        [TestMethod]
        public void TryBuyCard_Fails_WithZeroFunds()
        {
            // Arrange
            _player.Influence = 0;
            // Ensure card costs something
            Assert.IsGreaterThan(0, _cheapCard.Cost);

            // Act
            bool result = _market.TryBuyCard(_player, _cheapCard);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(0, _player.Influence, "Influence should remain 0, not negative.");
            Assert.DoesNotContain(_cheapCard, _player.DiscardPile);
        }
    }
}