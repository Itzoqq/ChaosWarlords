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

    [TestCategory("Unit")]
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
            _player = TestData.Players.PoorPlayer();

            // Setup dummy cards
            _cheapCard = TestData.Cards.CheapCard();
            _expensiveCard = TestData.Cards.ExpensiveCard();

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
        [DataRow(2, 5, true, 3)]    // Affordable
        [DataRow(10, 5, false, 5)]  // Too expensive
        [DataRow(3, 3, true, 0)]    // Exact funds
        [DataRow(2, 0, false, 0)]   // Zero funds
        public void TryBuyCard_HandlesVariousFundingScenarios(
            int cardCost,
            int playerInfluence,
            bool shouldSucceed,
            int expectedInfluenceAfter)
        {
            // Arrange
            var card = new CardBuilder().WithCost(cardCost).Build(); // Keep CardBuilder here as it's specifically testing cost variation
            _market.MarketRow.Add(card);
            _player.Influence = playerInfluence;

            // Act
            bool result = _market.TryBuyCard(_player, card, _stateManager);

            // Assert
            Assert.AreEqual(shouldSucceed, result);
            Assert.AreEqual(expectedInfluenceAfter, _player.Influence);
            Assert.AreEqual(shouldSucceed, _player.DiscardPile.Contains(card));

            if (shouldSucceed)
                CollectionAssert.DoesNotContain(_market.MarketRow, card);
        }

    }
}


