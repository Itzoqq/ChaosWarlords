using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Utilities; // For ActionState
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Managers;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Mechanics.Rules
{
    [TestClass]
    public class TargetingStateEngineTests
    {
        private CardRuleEngine GetRuleEngine()
        {
            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;
            var turnMgr = Substitute.For<ITurnManager>();
            var mapMgr = Substitute.For<IMapManager>();
            var actionSys = new ActionSystem(turnMgr, mapMgr, logger);
            var marketMgr = Substitute.For<IMarketManager>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = Substitute.For<IPlayerStateManager>();

            var context = new MatchContext(turnMgr, mapMgr, marketMgr, actionSys, cardDb, playerState, logger, 0);
            return new CardRuleEngine(context, logger);
        }
        [TestMethod]
        public void DetermineNextState_SingleTargetingEffect_ReturnsCorrectState()
        {
            // Arrange
            var effects = new List<CardEffect>
            {
                new CardEffect(EffectType.Assassinate, 0)
            };

            // Act
            // 1. Initial Call (Current = Normal) -> Should find Assassinate
            var state1 = TargetingStateEngine.DetermineNextState(effects, ActionState.Normal, false, GetRuleEngine());

            // Assert
            Assert.AreEqual(ActionState.TargetingAssassinate, state1);
        }

        [TestMethod]
        public void DetermineNextState_AfterCurrentState_ReturnsNormal_IfNoMore()
        {
            // Arrange
            var effects = new List<CardEffect>
            {
                new CardEffect(EffectType.Assassinate, 0)
            };

            // Act
            // 2. Second Call (Current = Assassinate) -> Should complete
            var nextState = TargetingStateEngine.DetermineNextState(effects, ActionState.TargetingAssassinate, false, GetRuleEngine());

            // Assert
            Assert.AreEqual(ActionState.Normal, nextState);
        }

        [TestMethod]
        public void DetermineNextState_ChainedEffects_ReturnsChildState()
        {
            // Arrange
            // Assassinate -> OnSuccess -> PlaceSpy
            var childEffect = new CardEffect(EffectType.PlaceSpy, 0);
            var rootEffect = new CardEffect(EffectType.Assassinate, 0) { OnSuccess = childEffect };
            var effects = new List<CardEffect> { rootEffect };

            // Act
            // 1. Start from Assassinate
            var nextState = TargetingStateEngine.DetermineNextState(effects, ActionState.TargetingAssassinate, false, GetRuleEngine());

            // Assert
            Assert.AreEqual(ActionState.TargetingPlaceSpy, nextState);
        }

        [TestMethod]
        public void DetermineNextState_SiblingEffects_ReturnsNextSibling()
        {
            // Arrange
            // Assassinate, then PlaceSpy (Siblings)
            var effects = new List<CardEffect>
            {
                new CardEffect(EffectType.Assassinate, 0),
                new CardEffect(EffectType.PlaceSpy, 0)
            };

            // Act
            // Start from Assassinate
            var nextState = TargetingStateEngine.DetermineNextState(effects, ActionState.TargetingAssassinate, false, GetRuleEngine());

            // Assert
            Assert.AreEqual(ActionState.TargetingPlaceSpy, nextState);
        }

        [TestMethod]
        public void DetermineNextState_SkippedParent_SkipsChildAndFindsSibling()
        {
            // Arrange
            // Root1: Assassinate -> OnSuccess: ReturnUnit (Child)
            // Root2: PlaceSpy (Sibling)
            // Scenario: User SKIPS Assassinate. Should NOT trigger ReturnUnit. Should trigger PlaceSpy.

            var child = new CardEffect(EffectType.ReturnUnit, 0);
            var root1 = new CardEffect(EffectType.Assassinate, 0) { OnSuccess = child };
            var root2 = new CardEffect(EffectType.PlaceSpy, 0);

            var effects = new List<CardEffect> { root1, root2 };

            // Act
            // Current = Assassinate, BUT isSkipped = true
            var nextState = TargetingStateEngine.DetermineNextState(effects, ActionState.TargetingAssassinate, isCurrentStateSkipped: true, ruleEngine: GetRuleEngine());

            // Assert
            Assert.AreEqual(ActionState.TargetingPlaceSpy, nextState);
        }

        [TestMethod]
        public void DetermineNextState_DeepRecursion_FindsNext()
        {
            // Arrange
            // Root -> Child (Assassinate) -> GrandChild (PlaceSpy)
            // We are at Assassinate. Next should be PlaceSpy.

            var grandChild = new CardEffect(EffectType.PlaceSpy, 0);
            var child = new CardEffect(EffectType.Assassinate, 0) { OnSuccess = grandChild };
            var root = new CardEffect(EffectType.GainResource, 0) { OnSuccess = child }; // GainResource is non-targeting

            var effects = new List<CardEffect> { root };

            // Act
            var nextState = TargetingStateEngine.DetermineNextState(effects, ActionState.TargetingAssassinate, false, GetRuleEngine());

            // Assert
            Assert.AreEqual(ActionState.TargetingPlaceSpy, nextState);
        }
    }
}
