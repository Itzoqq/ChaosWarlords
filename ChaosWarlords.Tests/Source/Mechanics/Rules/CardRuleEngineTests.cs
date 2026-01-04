using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities; // Added for Enums
using System.Collections.Generic;

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
            var uiMediator = Substitute.For<IUIEventMediator>();

            _context = new MatchContext(
                turnManager, 
                mapManager, 
                marketManager, 
                actionSystem, 
                cardDb, 
                playerState, 
                uiMediator, 
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
            _player.Hand.Add(sourceCard);

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
            _player.Hand.Add(sourceCard);
            _player.Hand.Add(otherCard);

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
            _player.Hand.Add(otherCard); // Hand count 1

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
    }
}
