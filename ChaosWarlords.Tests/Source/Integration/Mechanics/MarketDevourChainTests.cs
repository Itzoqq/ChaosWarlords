using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// Integration tests for Market devour chain resumption.
    /// Tests ensure Market Corruptor and similar cards correctly apply OnSuccess effects
    /// after devouring market cards (e.g., gaining influence).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MarketDevourChainTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private IGameLogger _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;

            _player = new Player(PlayerColor.Red);

            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(_player);

            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var actionSystem = Substitute.For<IActionSystem>();
            var playerStateManager = new PlayerStateManager(_logger);

            var cardDb = Substitute.For<ICardDatabase>();
            var uiMediator = Substitute.For<IUIEventMediator>();

            _context = new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                cardDb,
                playerStateManager,
                uiMediator,
                _logger
            );
        }

        [TestMethod]
        public void DevourMarketCard_WithOnSuccessEffect_AppliesSuccessorEffect()
        {
            // Arrange
            var sourceCard = new Card("market_corruptor", "Market Corruptor", 0, CardAspect.Oblivion, 0, 0, 0);
            var gainInfluenceEffect = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence);
            var devourEffect = new CardEffect(EffectType.Devour, 1)
            {
                TargetLocation = CardLocation.Market,
                OnSuccess = gainInfluenceEffect
            };
            sourceCard.AddEffect(devourEffect);

            var targetCard = new Card("market_card", "Market Card", 0, CardAspect.Neutral, 0, 0, 0);
            targetCard.Location = CardLocation.Market;

            _player.Influence = 0;

            // Create real MatchManager for this test
            var victoryManager = Substitute.For<IVictoryManager>();
            var realMatchManager = new MatchManager(_context, _logger, victoryManager);

            // Act
            realMatchManager.DevourMarketCard(targetCard, sourceCard);

            // Assert
            Assert.AreEqual(3, _player.Influence, "Player should gain 3 influence from OnSuccess effect");
            Assert.AreEqual(CardLocation.Void, targetCard.Location, "Target card should be voided");
        }

        [TestMethod]
        public void DevourMarketCard_WithoutSourceCard_DoesNotCrash()
        {
            // Arrange
            var targetCard = new Card("market_card", "Market Card", 0, CardAspect.Neutral, 0, 0, 0);
            targetCard.Location = CardLocation.Market;

            var victoryManager = Substitute.For<IVictoryManager>();
            var realMatchManager = new MatchManager(_context, _logger, victoryManager);

            // Act & Assert - should not throw
            realMatchManager.DevourMarketCard(targetCard, null);
            Assert.AreEqual(CardLocation.Void, targetCard.Location);
        }

        [TestMethod]
        public void DevourMarketCard_WithMultipleEffects_AppliesAllSuccessorEffects()
        {
            // Arrange
            var sourceCard = new Card("powerful_corruptor", "Powerful Corruptor", 0, CardAspect.Oblivion, 0, 0, 0);
            
            // Chain: Devour -> Gain Influence -> Gain Power
            var gainPowerEffect = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);
            var gainInfluenceEffect = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence)
            {
                OnSuccess = gainPowerEffect
            };
            var devourEffect = new CardEffect(EffectType.Devour, 1)
            {
                TargetLocation = CardLocation.Market,
                OnSuccess = gainInfluenceEffect
            };
            sourceCard.AddEffect(devourEffect);

            var targetCard = new Card("market_card", "Market Card", 0, CardAspect.Neutral, 0, 0, 0);
            targetCard.Location = CardLocation.Market;

            _player.Influence = 0;
            _player.Power = 0;

            var victoryManager = Substitute.For<IVictoryManager>();
            var realMatchManager = new MatchManager(_context, _logger, victoryManager);

            // Act
            realMatchManager.DevourMarketCard(targetCard, sourceCard);

            // Assert
            Assert.AreEqual(3, _player.Influence, "Player should gain 3 influence");
            Assert.AreEqual(2, _player.Power, "Player should gain 2 power from chained effect");
        }
    }
}
