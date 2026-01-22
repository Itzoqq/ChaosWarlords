using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Mechanics.Rules
{
    [TestClass]
    [TestCategory("Unit")]
    public class CardRuleEngineLookaheadTests
    {
        private CardRuleEngine _ruleEngine = null!;
        private MatchContext _context = null!;
        private Player _player = null!;
        private IMapManager _mapManager = null!;
        private IGameLogger _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            Tests.Utilities.TestLogger.Initialize();
            _logger = Tests.Utilities.TestLogger.Instance;

            _player = new Player(PlayerColor.Red);

            _mapManager = Substitute.For<IMapManager>();

            _context = new MatchContext(
                Substitute.For<ITurnManager>(),
                _mapManager, // Injected for specific queries
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                Substitute.For<IPlayerStateManager>(),
                Substitute.For<IUIEventMediator>(),
                _logger,
                12345
            );

            _ruleEngine = new CardRuleEngine(_context, _logger);

            // Wire logic: The rule engine delegates to MapManager for many checks.
            // Setup default success for specific methods
            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(true);
            _mapManager.HasValidMoveSource(Arg.Any<Player>()).Returns(true);
        }

        [TestMethod]
        public void IsEffectChainValid_SingleValidEffect_ReturnsTrue()
        {
            // Arrange
            var card = new Card("test", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 1, ResourceType.Power);

            // Act
            bool result = _ruleEngine.IsEffectChainValid(_player, effect, card);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsEffectChainValid_ChainValid_ReturnsTrue()
        {
            // Arrange: Devour (Cost) -> GainResource (Reward)
            // Note: Devour defaults to true if player has Hand cards (default) or we mock it.
            // Player starts with empty hand, so we add one.
            _player.AddToHand(new Card("h1", "Hand", 0, CardAspect.Neutral, 0, 0, 0));

            var card = new Card("test", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Devour, 1)
            {
                OnSuccess = new CardEffect(EffectType.GainResource, 1, ResourceType.Power)
            };

            // Act
            bool result = _ruleEngine.IsEffectChainValid(_player, effect, card);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsEffectChainValid_ChainInvalidDeep_ReturnsFalse()
        {
            // Arrange: Devour (Valid) -> Assassinate (Invalid/Impossible)
            _player.AddToHand(new Card("h1", "Hand", 0, CardAspect.Neutral, 0, 0, 0));

            // Mock Assassinate failing
            _mapManager.HasValidAssassinationTarget(_player).Returns(false);

            var card = new Card("test", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Devour, 1)
            {
                OnSuccess = new CardEffect(EffectType.Assassinate, 1) // This should fail
            };

            // Act
            bool result = _ruleEngine.IsEffectChainValid(_player, effect, card);

            // Assert
            Assert.IsFalse(result, "Should fail because dependent effect is impossible.");
        }

        [TestMethod]
        public void IsEffectChainValid_ChainInvalidBase_ReturnsFalse()
        {
            // Arrange: Devour (Invalid - No Cards) -> GainResource (Valid)
            // Default setup: Player hand is empty.
            _player.ClearHand();

            var card = new Card("test", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Devour, 1)
            {
                TargetLocation = CardLocation.Hand,
                OnSuccess = new CardEffect(EffectType.GainResource, 1)
            };

            // Act
            bool result = _ruleEngine.IsEffectChainValid(_player, effect, card);

            // Assert
            Assert.IsFalse(result, "Should fail because base cost cannot be paid (Empty Hand).");
        }

        [TestMethod]
        public void IsEffectChainValid_NestedRecursion_ReturnsFalse()
        {
            // Arrange: Devour -> Draw -> Assassinate (Fail)
            _player.AddToHand(new Card("h1", "Hand", 0, CardAspect.Neutral, 0, 0, 0));
            _mapManager.HasValidAssassinationTarget(_player).Returns(false);

            var card = new Card("test", "Test", 0, CardAspect.Neutral, 0, 0, 0);

            var deepEffect = new CardEffect(EffectType.Assassinate, 1);
            var midEffect = new CardEffect(EffectType.DrawCard, 1) { OnSuccess = deepEffect };
            var rootEffect = new CardEffect(EffectType.Devour, 1) { OnSuccess = midEffect };

            // Act
            bool result = _ruleEngine.IsEffectChainValid(_player, rootEffect, card);

            // Assert
            Assert.IsFalse(result, "Should fail because 3rd level effect is impossible.");
        }
    }
}
