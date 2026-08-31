using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Rules.Strategies;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Direct tests for the IEffectStrategy implementations (Mechanics/Rules/Strategies/).
    /// Before this file, 6 of these 7 classes had zero direct test coverage - "very plausibly
    /// covered indirectly through the command/integration tests that exercise them end-to-end,
    /// but that's an assumption, not verified line-by-line" (planning.txt, 2026-08-31
    /// architecture review). Measured with real code coverage (dotnet test --collect:"XPlat
    /// Code Coverage"), not assumed: indirect coverage turned out to be genuinely strong for
    /// most of them (~88% lines), but DefaultStrategy.HasValidTargets was NEVER reached by any
    /// existing test or, in fact, by any current production call site at all - every caller of
    /// CardRuleEngine.HasValidTargets/IsEffectChainValid checks IsTargetingEffect first, and
    /// DefaultStrategy.IsTargetingEffect is always false, so its HasValidTargets body was
    /// structurally unreachable in practice. DevourStrategy - excluded from the original "6
    /// classes" list on the assumption DevourStrategyFactoryTests.cs already covered it
    /// directly - turned out to have exactly one trivial assertion there (IsTargetingEffect),
    /// not real coverage of GetTargetingState/HasValidTargets; included here too.
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class EffectStrategiesTests
    {
        private static MatchContext BuildContext(IMarketManager? marketManager = null)
        {
            var builder = new MatchContextBuilder();
            if (marketManager != null) builder.WithMarketManager(marketManager);
            return builder.Build();
        }

        #region AssassinateStrategy

        [TestMethod]
        public void AssassinateStrategy_EffectType_IsAssassinate()
        {
            Assert.AreEqual(EffectType.Assassinate, new AssassinateStrategy().EffectType);
        }

        [TestMethod]
        public void AssassinateStrategy_IsTargetingEffect_IsTrue()
        {
            Assert.IsTrue(new AssassinateStrategy().IsTargetingEffect);
        }

        [TestMethod]
        public void AssassinateStrategy_GetTargetingState_ReturnsTargetingAssassinate()
        {
            var state = new AssassinateStrategy().GetTargetingState(new CardEffect(EffectType.Assassinate, 0));
            Assert.AreEqual(ActionState.TargetingAssassinate, state);
        }

        [TestMethod]
        public void AssassinateStrategy_HasValidTargets_DelegatesToMapManagerHasValidAssassinationTarget()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().Build();
            mapManager.HasValidAssassinationTarget(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            bool result = new AssassinateStrategy().HasValidTargets(context, player, null);

            Assert.IsTrue(result);
            mapManager.Received(1).HasValidAssassinationTarget(player);
        }

        [TestMethod]
        public void AssassinateStrategy_HasValidTargets_NoValidTarget_ReturnsFalse()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().Build();
            mapManager.HasValidAssassinationTarget(player).Returns(false);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsFalse(new AssassinateStrategy().HasValidTargets(context, player, null));
        }

        #endregion

        #region ReturnUnitStrategy

        [TestMethod]
        public void ReturnUnitStrategy_EffectType_IsReturnUnit()
        {
            Assert.AreEqual(EffectType.ReturnUnit, new ReturnUnitStrategy().EffectType);
        }

        [TestMethod]
        public void ReturnUnitStrategy_IsTargetingEffect_IsTrue()
        {
            Assert.IsTrue(new ReturnUnitStrategy().IsTargetingEffect);
        }

        [TestMethod]
        public void ReturnUnitStrategy_GetTargetingState_ReturnsTargetingReturn()
        {
            var state = new ReturnUnitStrategy().GetTargetingState(new CardEffect(EffectType.ReturnUnit, 0));
            Assert.AreEqual(ActionState.TargetingReturn, state);
        }

        [TestMethod]
        public void ReturnUnitStrategy_HasValidTargets_DelegatesToMapManagerHasValidReturnTroopTarget()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().Build();
            mapManager.HasValidReturnTroopTarget(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsTrue(new ReturnUnitStrategy().HasValidTargets(context, player, null));
            mapManager.Received(1).HasValidReturnTroopTarget(player);
        }

        #endregion

        #region SupplantStrategy

        [TestMethod]
        public void SupplantStrategy_EffectType_IsSupplant()
        {
            Assert.AreEqual(EffectType.Supplant, new SupplantStrategy().EffectType);
        }

        [TestMethod]
        public void SupplantStrategy_IsTargetingEffect_IsTrue()
        {
            Assert.IsTrue(new SupplantStrategy().IsTargetingEffect);
        }

        [TestMethod]
        public void SupplantStrategy_GetTargetingState_ReturnsTargetingSupplant()
        {
            var state = new SupplantStrategy().GetTargetingState(new CardEffect(EffectType.Supplant, 0));
            Assert.AreEqual(ActionState.TargetingSupplant, state);
        }

        [TestMethod]
        public void SupplantStrategy_HasValidTargets_NoTroopsInBarracks_ReturnsFalse()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().WithTroops(0).Build();
            mapManager.HasValidAssassinationTarget(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsFalse(new SupplantStrategy().HasValidTargets(context, player, null));
        }

        [TestMethod]
        public void SupplantStrategy_HasValidTargets_NoAssassinationTarget_ReturnsFalse()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().WithTroops(3).Build();
            mapManager.HasValidAssassinationTarget(player).Returns(false);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsFalse(new SupplantStrategy().HasValidTargets(context, player, null));
        }

        [TestMethod]
        public void SupplantStrategy_HasValidTargets_TroopsAndTargetBothAvailable_ReturnsTrue()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().WithTroops(3).Build();
            mapManager.HasValidAssassinationTarget(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsTrue(new SupplantStrategy().HasValidTargets(context, player, null));
        }

        #endregion

        #region MoveUnitStrategy

        [TestMethod]
        public void MoveUnitStrategy_EffectType_IsMoveUnit()
        {
            Assert.AreEqual(EffectType.MoveUnit, new MoveUnitStrategy().EffectType);
        }

        [TestMethod]
        public void MoveUnitStrategy_IsTargetingEffect_IsTrue()
        {
            Assert.IsTrue(new MoveUnitStrategy().IsTargetingEffect);
        }

        [TestMethod]
        public void MoveUnitStrategy_GetTargetingState_ReturnsTargetingMoveSource()
        {
            var state = new MoveUnitStrategy().GetTargetingState(new CardEffect(EffectType.MoveUnit, 0));
            Assert.AreEqual(ActionState.TargetingMoveSource, state);
        }

        [TestMethod]
        public void MoveUnitStrategy_HasValidTargets_DelegatesToMapManagerHasValidMoveSource()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().Build();
            mapManager.HasValidMoveSource(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsTrue(new MoveUnitStrategy().HasValidTargets(context, player, null));
            mapManager.Received(1).HasValidMoveSource(player);
        }

        #endregion

        #region PlaceSpyStrategy

        [TestMethod]
        public void PlaceSpyStrategy_EffectType_IsPlaceSpy()
        {
            Assert.AreEqual(EffectType.PlaceSpy, new PlaceSpyStrategy().EffectType);
        }

        [TestMethod]
        public void PlaceSpyStrategy_IsTargetingEffect_IsTrue()
        {
            Assert.IsTrue(new PlaceSpyStrategy().IsTargetingEffect);
        }

        [TestMethod]
        public void PlaceSpyStrategy_GetTargetingState_ReturnsTargetingPlaceSpy()
        {
            var state = new PlaceSpyStrategy().GetTargetingState(new CardEffect(EffectType.PlaceSpy, 0));
            Assert.AreEqual(ActionState.TargetingPlaceSpy, state);
        }

        [TestMethod]
        public void PlaceSpyStrategy_HasValidTargets_NoSpiesInBarracks_ReturnsFalse()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().WithSpies(0).Build();
            mapManager.HasValidPlaceSpyTarget(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsFalse(new PlaceSpyStrategy().HasValidTargets(context, player, null));
        }

        [TestMethod]
        public void PlaceSpyStrategy_HasValidTargets_NoValidSite_ReturnsFalse()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().WithSpies(2).Build();
            mapManager.HasValidPlaceSpyTarget(player).Returns(false);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsFalse(new PlaceSpyStrategy().HasValidTargets(context, player, null));
        }

        [TestMethod]
        public void PlaceSpyStrategy_HasValidTargets_SpiesAndSiteBothAvailable_ReturnsTrue()
        {
            var mapManager = Substitute.For<IMapManager>();
            var player = new PlayerBuilder().WithSpies(2).Build();
            mapManager.HasValidPlaceSpyTarget(player).Returns(true);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.IsTrue(new PlaceSpyStrategy().HasValidTargets(context, player, null));
        }

        #endregion

        #region DefaultStrategy

        [TestMethod]
        public void DefaultStrategy_EffectType_IsNone()
        {
            Assert.AreEqual(EffectType.None, new DefaultStrategy().EffectType);
        }

        [TestMethod]
        public void DefaultStrategy_IsTargetingEffect_IsFalse()
        {
            // This is exactly why HasValidTargets below is unreachable through every current
            // production call site (CardRuleEngine.IsEffectChainValid, ActionExecutionEngine.
            // ProcessOptionalEffect's lookahead) - both check IsTargetingEffect before ever
            // calling HasValidTargets.
            Assert.IsFalse(new DefaultStrategy().IsTargetingEffect);
        }

        [TestMethod]
        public void DefaultStrategy_GetTargetingState_ReturnsNormal()
        {
            var state = new DefaultStrategy().GetTargetingState(new CardEffect(EffectType.GainResource, 1));
            Assert.AreEqual(ActionState.Normal, state);
        }

        [TestMethod]
        public void DefaultStrategy_HasValidTargets_AlwaysReturnsTrue_RegardlessOfMapState()
        {
            // Non-targeting effects (GainResource, DrawCard, Promote, ...) are always valid
            // target-wise - there's nothing to target. Assert this holds even with a MapManager
            // that would say "no" for every other strategy's targeting check, since nothing
            // about DefaultStrategy should depend on map state at all.
            var mapManager = Substitute.For<IMapManager>();
            mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(false);
            mapManager.HasValidMoveSource(Arg.Any<Player>()).Returns(false);
            mapManager.HasValidPlaceSpyTarget(Arg.Any<Player>()).Returns(false);
            mapManager.HasValidReturnTroopTarget(Arg.Any<Player>()).Returns(false);
            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();
            var player = new PlayerBuilder().Build();

            Assert.IsTrue(new DefaultStrategy().HasValidTargets(context, player, null));
        }

        #endregion

        #region DevourStrategy

        [TestMethod]
        public void DevourStrategy_EffectType_IsDevour()
        {
            Assert.AreEqual(EffectType.Devour, new DevourStrategy().EffectType);
        }

        [TestMethod]
        public void DevourStrategy_IsTargetingEffect_IsTrue()
        {
            Assert.IsTrue(new DevourStrategy().IsTargetingEffect);
        }

        [TestMethod]
        [DataRow(CardLocation.Market, ActionState.TargetingDevourMarket)]
        [DataRow(CardLocation.InnerCircle, ActionState.TargetingDevourInnerCircle)]
        [DataRow(CardLocation.Self, ActionState.Normal)]
        [DataRow(CardLocation.Hand, ActionState.TargetingDevourHand)]
        [DataRow(CardLocation.Deck, ActionState.TargetingDevourHand)] // Falls through to the default arm
        public void DevourStrategy_GetTargetingState_MapsTargetLocationToActionState(CardLocation targetLocation, ActionState expected)
        {
            var effect = new CardEffect(EffectType.Devour, 0) { TargetLocation = targetLocation };

            var state = new DevourStrategy().GetTargetingState(effect);

            Assert.AreEqual(expected, state);
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_NullSourceCard_FallsBackToHandTargets()
        {
            var player = new PlayerBuilder().WithCardsInHand(TestData.Cards.CheapCard()).Build();
            var context = BuildContext();

            Assert.IsTrue(new DevourStrategy().HasValidTargets(context, player, null));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_SourceCardWithNoDevourEffect_FallsBackToHandTargets()
        {
            var sourceCard = new CardBuilder().WithEffect(EffectType.GainResource, 1).Build();
            var player = new PlayerBuilder().WithCardsInHand(TestData.Cards.CheapCard()).Build();
            var context = BuildContext();

            Assert.IsTrue(new DevourStrategy().HasValidTargets(context, player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationSelf_AlwaysTrue()
        {
            var sourceCard = new CardBuilder().Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Self });
            var player = new PlayerBuilder().Build(); // Empty hand, deck, inner circle

            Assert.IsTrue(new DevourStrategy().HasValidTargets(BuildContext(), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationMarket_DelegatesToMarketRow()
        {
            var sourceCard = new CardBuilder().Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Market });
            var player = new PlayerBuilder().Build();
            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card> { TestData.Cards.CheapCard() });

            Assert.IsTrue(new DevourStrategy().HasValidTargets(BuildContext(marketManager), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationMarket_EmptyMarket_ReturnsFalse()
        {
            var sourceCard = new CardBuilder().Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Market });
            var player = new PlayerBuilder().Build();
            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card>());

            Assert.IsFalse(new DevourStrategy().HasValidTargets(BuildContext(marketManager), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationDeck_DelegatesToPlayerDeck()
        {
            var sourceCard = new CardBuilder().Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Deck });
            var player = new PlayerBuilder().WithCardsInDeck(TestData.Cards.CheapCard()).Build();

            Assert.IsTrue(new DevourStrategy().HasValidTargets(BuildContext(), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationDeck_EmptyDeck_ReturnsFalse()
        {
            var sourceCard = new CardBuilder().Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Deck });
            var player = new PlayerBuilder().Build(); // Empty deck

            Assert.IsFalse(new DevourStrategy().HasValidTargets(BuildContext(), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationInnerCircle_DelegatesToPlayerInnerCircle()
        {
            var sourceCard = new CardBuilder().Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.InnerCircle });
            var player = new PlayerBuilder().WithCardsInInnerCircle(TestData.Cards.CheapCard()).Build();

            Assert.IsTrue(new DevourStrategy().HasValidTargets(BuildContext(), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationHand_ExcludesTheSourceCardItself()
        {
            // HasHandTargets checks player.Hand.Any(c => c != sourceCard) - a hand containing
            // ONLY the source card itself should not count as a valid target.
            var sourceCard = new CardBuilder().WithName("only_card_in_hand").Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Hand });
            var player = new PlayerBuilder().WithCardsInHand(sourceCard).Build();

            Assert.IsFalse(new DevourStrategy().HasValidTargets(BuildContext(), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_TargetLocationHand_AnotherCardInHand_ReturnsTrue()
        {
            var sourceCard = new CardBuilder().WithName("source").Build();
            sourceCard.Effects.Add(new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Hand });
            var otherCard = new CardBuilder().WithName("other").Build();
            var player = new PlayerBuilder().WithCardsInHand(sourceCard, otherCard).Build();

            Assert.IsTrue(new DevourStrategy().HasValidTargets(BuildContext(), player, sourceCard));
        }

        [TestMethod]
        public void DevourStrategy_HasValidTargets_DevourEffectNestedInOnSuccessChain_FindsItRecursively()
        {
            // FindFirstEffect walks OnSuccess chains looking for a Devour effect - not just the
            // card's top-level effect list. E.g. "Gain Power, then Devour a card from Market".
            var sourceCard = new CardBuilder().Build();
            var devourEffect = new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Market };
            var gainEffect = new CardEffect(EffectType.GainResource, 1) { OnSuccess = devourEffect };
            sourceCard.Effects.Add(gainEffect);

            var player = new PlayerBuilder().Build();
            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card> { TestData.Cards.CheapCard() });

            Assert.IsTrue(new DevourStrategy().HasValidTargets(BuildContext(marketManager), player, sourceCard));
        }

        #endregion
    }
}
