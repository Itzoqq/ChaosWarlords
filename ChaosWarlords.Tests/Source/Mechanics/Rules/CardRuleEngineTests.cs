using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities; // Added for Enums

namespace ChaosWarlords.Tests.Source.Mechanics.Rules
{
    [TestClass]
    [TestCategory("Unit")]
    public class CardRuleEngineTests
    {
        private CardRuleEngine _ruleEngine = null!;
        private MatchContext _context = null!;
        private IGameLogger _logger = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();

            // Mock MatchContext dependencies
            var turnManager = Substitute.For<ITurnManager>();
            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var actionSystem = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = Substitute.For<IPlayerStateManager>();

            _context = new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                cardDb,
                playerState,
                _logger,
                12345
            );

            _ruleEngine = new CardRuleEngine(_context, _logger);
            _player = new Player(PlayerColor.Red);
        }

        [TestMethod]
        public void HasValidTargets_Devour_ReturnsFalse_WhenHandEmpty()
        {
            // Act
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void HasValidTargets_Devour_ReturnsFalse_WhenHandHasOnlySourceCard()
        {
            // Arrange
            var sourceCard = new Card("c1", "Source", 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = CardLocation.Hand
            };
            _player.AddToHand(sourceCard);

            // Act
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour, sourceCard);

            // Assert
            Assert.IsFalse(result, "Should fail if the only card is the source card");
        }

        [TestMethod]
        public void HasValidTargets_Devour_ReturnsTrue_WhenHandHasOtherCards()
        {
            // Arrange
            var sourceCard = new Card("c1", "Source", 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = CardLocation.Hand
            };
            var otherCard = new Card("c2", "Other", 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = CardLocation.Hand
            };
            _player.AddToHand(sourceCard);
            _player.AddToHand(otherCard);

            // Act
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour, sourceCard);

            // Assert
            Assert.IsTrue(result, "Should pass if there is another card (c2)");
        }

        [TestMethod]
        public void HasValidTargets_Devour_ReturnsTrue_WhenSourceCardIsNotInHand()
        {
            // Arrange: Source card is "Played" (e.g. on stack), Hand has 1 card
            var sourceCard = new Card("c1", "Source", 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = CardLocation.Played
            };
            var otherCard = new Card("c2", "Other", 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = CardLocation.Hand
            };
            _player.AddToHand(otherCard); // Hand count 1

            // Act
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour, sourceCard);

            // Assert
            Assert.IsTrue(result);
        }
        [TestMethod]
        public void HasValidTargets_DevourMarket_ReturnsTrue_WhenMarketHasCards()
        {
            // Arrange
            var sourceCard = new Card("c1", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.Market });

            // Mock Market with one card
            var marketCards = new List<Card>
            {
                new Card("m1", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0)
            };
            _context.MarketManager.MarketRow.Returns(marketCards);

            // Act
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour, sourceCard);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HasValidTargets_DevourMarket_ReturnsFalse_WhenMarketEmpty()
        {
            // Arrange
            var sourceCard = new Card("c1", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.Market });

            // Mock Empty Market
            var marketCards = new List<Card>(); // Empty list
            _context.MarketManager.MarketRow.Returns(marketCards);

            // Act
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour, sourceCard);

            // Assert
            Assert.IsFalse(result);
        }
        [TestMethod]
        public void HasValidTargets_NestedSelfDevour_ReturnsTrue()
        {
            // Arrange
            var sourceCard = new Card("c1", "ConditionalSelf", 0, CardAspect.Neutral, 0, 0, 0)
            {
                Location = CardLocation.Hand
            };
            
            // Effect Chain: Conditional -> OnSuccess: Devour(Self)
            var devourSelfEffect = new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.Self };
            var conditionalEffect = new CardEffect(EffectType.GainResource, 1) // Dummy condition
            { 
                 // Condition = null (Default is always true effectively for simple tests if we don't evaluate it)
                 OnSuccess = devourSelfEffect
            };
            sourceCard.Effects.Add(conditionalEffect);

            _player.AddToHand(sourceCard);

            // Act
            // We ask if we have valid targets for Devour (the inner effect type)
            // But we pass the sourceCard which wraps it.
            bool result = _ruleEngine.HasValidTargets(_player, EffectType.Devour, sourceCard);

            // Assert
            // Current Bug: Logic falls back to HasHandTargets because it doesn't see Devour in root effects.
            // HasHandTargets sees 1 card (source) and returns false.
            // Expected Fix: It should find the nested effect, see Target=Self, and return True.
            // Bug Fixed: Logic deep searches for effect.
            Assert.IsTrue(result, "Should pass because deep search finds nested Self target");
        }
    }
}
