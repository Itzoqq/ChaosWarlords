using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using NSubstitute;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
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
        private IPlayerStateManager _stateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();

            // Setup dummy cards
            _cheapCard = new CardBuilder().WithName("c1").WithCost(2).WithAspect(CardAspect.Neutral).WithPower(1).WithInfluence(1).WithVP(0).Build();
            _expensiveCard = new CardBuilder().WithName("c2").WithCost(10).WithAspect(CardAspect.Neutral).WithPower(1).WithInfluence(1).WithVP(0).Build();

            // 1. Create the Mock using NSubstitute
            _mockDb = Substitute.For<ICardDatabase>();

            // 2. Configure the Mock behavior
            var deck = new List<Card> { _cheapCard, _expensiveCard };
            _mockDb.GetAllMarketCards().Returns(deck);

            // 4. Use Real StateManager (or Mock if strictly isolating, but Real is better for logic verification)
            _stateManager = new PlayerStateManager();

            // 3. Inject the Mock
            _market = new MarketManager(_mockDb);
        }

        [TestMethod]
        public void TryBuyCard_Succeeds_WhenAffordable()
        {
            // Use StateManager or direct set for setup ??
            // Actually, tests should simulate flow.
            _player.Influence = 5;

            bool result = _market.TryBuyCard(_player, _cheapCard, _stateManager);

            Assert.IsTrue(result);
            Assert.AreEqual(3, _player.Influence);
            Assert.IsTrue(_player.DiscardPile.Contains(_cheapCard));
            CollectionAssert.DoesNotContain(_market.MarketRow, _cheapCard);
        }

        [TestMethod]
        public void TryBuyCard_Fails_WhenTooExpensive()
        {
            _player.Influence = 5;

            bool result = _market.TryBuyCard(_player, _expensiveCard, _stateManager);

            Assert.IsFalse(result);
            Assert.AreEqual(5, _player.Influence);
            Assert.IsFalse(_player.DiscardPile.Contains(_expensiveCard));
        }

        [TestMethod]
        public void RefillMarket_Refills_WhenCardPurchased_AndDeckHasReserves()
        {
            // Arrange
            var cards = new List<Card>();
            for (int i = 0; i < 6; i++)
            {
                cards.Add(new Card($"fill{i}", "Filler", 1, CardAspect.Neutral, 0, 0, 0));
            }
            var reserveCard = new CardBuilder().WithName("reserve").WithCost(1).WithAspect(CardAspect.Neutral).WithPower(0).WithInfluence(0).WithVP(0).Build();
            cards.Add(reserveCard);

            var localMockDb = Substitute.For<ICardDatabase>();
            localMockDb.GetAllMarketCards().Returns(new List<Card>(cards));

            var localMarket = new MarketManager(localMockDb);

            _player.Influence = 100;

            // Handle Random Shuffle
            var cardsInRow = localMarket.MarketRow;
            var cardLeftInDeck = cards.Except(cardsInRow).FirstOrDefault();

            Assert.IsNotNull(cardLeftInDeck, "Should be 1 card left in the deck (7 total, 6 in row).");
            Assert.HasCount(6, localMarket.MarketRow);

            // Act: Buy one card to trigger refill
            var cardToBuy = localMarket.MarketRow[0];
            bool bought = localMarket.TryBuyCard(_player, cardToBuy, _stateManager);

            // Assert
            Assert.IsTrue(bought);
            Assert.HasCount(6, localMarket.MarketRow, "Market should refill back to 6");
            CollectionAssert.Contains(localMarket.MarketRow, cardLeftInDeck, "The specific card from the deck should have entered the row.");
        }

        [TestMethod]
        public void TryBuyCard_Succeeds_WithExactFunds()
        {
            // Arrange
            var exactCard = new CardBuilder().WithName("c_exact").WithCost(3).WithAspect(CardAspect.Neutral).WithPower(1).WithInfluence(0).WithVP(0).Build();

            // Manually add to the row (bypassing the deck/mock for this specific setup)
            _market.MarketRow.Add(exactCard);
            _player.Influence = 3;

            // Act
            bool result = _market.TryBuyCard(_player, exactCard, _stateManager);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _player.Influence);
            Assert.IsTrue(_player.DiscardPile.Contains(exactCard));
        }

        [TestMethod]
        public void TryBuyCard_Fails_WithZeroFunds()
        {
            _player.Influence = 0;

            bool result = _market.TryBuyCard(_player, _cheapCard, _stateManager);

            Assert.IsFalse(result);
            Assert.AreEqual(0, _player.Influence);
            Assert.IsFalse(_player.DiscardPile.Contains(_cheapCard));
        }
    }
}


