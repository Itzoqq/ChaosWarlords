using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Source.Systems
{
    // Covers CardEffectProcessor - the effect-tree construction/repeat-expansion pipeline
    // (ResolveEffects and everything it calls). Effect-APPLICATION tests (ApplyEffect and its
    // per-EffectType handlers) live in CardEffectApplierTests.cs instead, mirroring the
    // production CardEffectProcessor/CardEffectApplier split.
    [TestClass]

    [TestCategory("Unit")]
    public class CardEffectProcessorTests
    {
        private CardEffectProcessor _processor = null!;
        private MatchContext _context = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            Tests.Utilities.TestLogger.Initialize();
            _processor = new CardEffectProcessor();
            _player = TestData.Players.PoorPlayer();

            var turnSub = Substitute.For<ITurnManager>();
            turnSub.ActivePlayer.Returns(_player);
            // We need a real TurnContext for promotions
            turnSub.CurrentTurnContext.Returns(new TurnContext(_player, Tests.Utilities.TestLogger.Instance));

            _context = new MatchContext(
                turnSub,
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                new PlayerStateManager(Tests.Utilities.TestLogger.Instance), // <--- Use real StateManager for logic testing
                Tests.Utilities.TestLogger.Instance
            );

            // Inject Partial Mock (Spy) for CardRuleEngine to allow mocking virtual methods if needed
            // But verify behavior primarily through Logic and ActionSystem mocks
            var ruleEngineSpy = Substitute.ForPartsOf<CardRuleEngine>(_context, Tests.Utilities.TestLogger.Instance);

            // Inject via Reflection (Private Setter)
            typeof(MatchContext).GetProperty("CardRuleEngine")!.SetValue(_context, ruleEngineSpy);
        }


        [TestMethod]
        public void ResolveEffects_GainPower_AddsToPlayer()
        {
            var card = TestData.Cards.PowerCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            // Assert: Pushed to Stack
            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.GainResource));
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_SkippedWithoutFocus()
        {
            var card = TestData.Cards.FocusPowerCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_AppliedWithFocus()
        {
            var card = TestData.Cards.FocusPowerCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.GainResource));
        }

        [TestMethod]
        public void ResolveEffects_Promote_AddsCreditToTurnContext()
        {
            var card = TestData.Cards.NobleCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.Promote));
        }

        [TestMethod]
        public void ResolveEffects_GainInfluence_AddsToPlayer()
        {
            var card = TestData.Cards.InfluenceCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.GainResource));
        }

        [TestMethod]
        public void ResolveEffects_DrawCard_CallsPlayerDrawCards()
        {
            var card = TestData.Cards.DrawCard();

            // Add cards to deck so DrawCards can work
            _player.DeckManager.AddToTop(TestData.Cards.CheapCard());
            _player.DeckManager.AddToTop(TestData.Cards.CheapCard());

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.DrawCard));
        }


        [TestMethod]
        [DataRow(EffectType.MoveUnit, ActionState.TargetingMoveSource, "MoveSource")]
        [DataRow(EffectType.Assassinate, ActionState.TargetingAssassinate, "Assassination")]
        [DataRow(EffectType.ReturnUnit, ActionState.TargetingReturn, "ReturnTroop")]
        public void ResolveEffects_WithValidTargets_StartsTargeting(
            EffectType effectType,
            ActionState expectedState,
            string validationMethod)
        {
            var card = effectType switch
            {
                EffectType.MoveUnit => TestData.Cards.MoveUnitCard(),
                EffectType.Assassinate => TestData.Cards.AssassinCard(),
                EffectType.ReturnUnit => TestData.Cards.ReturnUnitCard(),
                _ => TestData.Cards.CheapCard()
            };

            SetupValidTargets(validationMethod, hasTargets: true);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            // Assert: Pushed to Stack with correct state
            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.EffectType == expectedState));
        }

        [TestMethod]
        [DataRow(EffectType.MoveUnit, "MoveSource")]
        [DataRow(EffectType.Assassinate, "Assassination")]
        [DataRow(EffectType.ReturnUnit, "ReturnTroop")]
        public void ResolveEffects_NoValidTargets_DoesNotStartTargeting(
            EffectType effectType,
            string validationMethod)
        {
            var card = effectType switch
            {
                EffectType.MoveUnit => TestData.Cards.MoveUnitCard(),
                EffectType.Assassinate => TestData.Cards.AssassinCard(),
                EffectType.ReturnUnit => TestData.Cards.ReturnUnitCard(),
                _ => TestData.Cards.CheapCard()
            };

            SetupValidTargets(validationMethod, hasTargets: false);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        private void SetupValidTargets(string validationMethod, bool hasTargets)
        {
            switch (validationMethod)
            {
                // NOTE: CardEffectProcessor now uses CardRuleEngine for validation.
                // We must mock CardRuleEngine via the Context (which we didn't mock explicitly in Setup but passed a real one via MatchContext constructor?)
                // Wait, MatchContext constructor takes ICardRuleEngine?
                // Checks Setup: MatchContext has ICardRuleEngine?
                // No, MatchContext constructs CardRuleEngine internally?
                // Checking MatchContext.cs...
                // Mocking MapManager calls is valid IF CardRuleEngine delegates to MapManager.
                // Assuming CardRuleEngine delegates to Manager calls, these mocks work.
                // Logic trace: HasValidAssassinationTarget -> MapManager.HasValidAssassinationTarget.

                case "MoveSource":
                    _context.MapManager.HasValidMoveSource(_player).Returns(hasTargets);
                    break;
                case "Assassination":
                    _context.MapManager.HasValidAssassinationTarget(_player).Returns(hasTargets);
                    break;
                case "ReturnTroop":
                    _context.MapManager.HasValidReturnTroopTarget(_player).Returns(hasTargets);
                    break;
            }
        }

        [TestMethod]
        public void ResolveEffects_Supplant_DelegatesToActionSystem()
        {
            var card = TestData.Cards.SupplantCard();

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);
            _player.TroopsInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.EffectType == ActionState.TargetingSupplant));
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_WithValidTargetsAndSpies_StartsTargeting()
        {
            var card = TestData.Cards.PlaceSpyCard();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.EffectType == ActionState.TargetingPlaceSpy));
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoSpies_DoesNotStartTargeting()
        {
            var card = TestData.Cards.PlaceSpyCard();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 0;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoValidTargets_DoesNotStartTargeting()
        {
            var card = TestData.Cards.PlaceSpyCard();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(false);
            _player.SpiesInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        [TestMethod]
        public void ResolveEffects_Devour_WithCardsInHand_StartsDevour()
        {
            var card = TestData.Cards.DevourCard();

            _player.AddToHand(TestData.Cards.CheapCard());

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.EffectType == ActionState.TargetingDevourHand));
        }

        [TestMethod]
        public void ResolveEffects_Devour_EmptyHand_DoesNotStartDevour()
        {
            var card = TestData.Cards.DevourCard();
            // Hand Empty

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        [TestMethod]
        public void ResolveEffects_MultipleEffects_AllApplied()
        {
            // Setup card with multiple effects using TestData for initial card then adding more?
            // Or just add a MultiEffectCard to TestData.
            var card = TestData.Cards.NobleCard(); // Promote 1
            card.Effects.Add(new CardEffect(EffectType.GainResource, 2, ResourceType.Power));
            card.Effects.Add(new CardEffect(EffectType.GainResource, 3, ResourceType.Influence));

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            // Expecting 3 Calls
            _context.ActionSystem.Received(3).PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }
        [TestMethod]
        public void ResolveEffects_SkipsOptionalPopup_WhenNoValidTargets()
        {
            // Arrange
            // Create a card with an optional Devour effect
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.IsOptional = true; // Ensure it's optional for this test

            // Ensure Hand is empty (source card is explicitly excluded by definition in HasValidTargets)
            _player.ClearHand();

            // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true, Tests.Utilities.TestLogger.Instance);

            // Assert
            // Should NOT push effect if logic validation works (RequiresInput + InvalidTarget = Skip)
            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        [TestMethod]
        public void ResolveEffects_ShowsOptionalPopup_WhenValidTargetsExist()
        {
            // Arrange
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.IsOptional = true;

            // Add another card to make targets valid
            _player.ClearHand();
            _player.AddToHand(new Card("dummy", "Dummy", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Hand });

            // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true, Tests.Utilities.TestLogger.Instance);

            // Assert
            // Should Push Effect with RequiresInput = true
            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.EffectType == ActionState.TargetingDevourHand && c.RequiresInput));
        }

        #region Alternative (Choose-one) Effect Tests

        [TestMethod]
        public void ResolveEffects_WithAlternative_NoValidTargets_PushesAlternativeInstead()
        {
            // Arrange: a mandatory Devour(Hand) effect with an Alternative fallback, but the
            // hand is empty (no valid Devour targets) - the top-level HasValidTargets
            // pre-check must push the Alternative instead of silently skipping (the bug this
            // primitive fixes: Wight played with an empty hand used to grant nothing at all).
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.Alternative = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);
            _player.ClearHand();

            // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            // Assert
            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(
                c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.GainResource));
        }

        [TestMethod]
        public void ResolveEffects_WithAlternative_DeclinedViaCallback_PushesAlternative()
        {
            // Arrange: an optional Devour(Hand) effect with an Alternative, valid targets
            // present. Simulates the "player declined the popup" (or ActionExecutionEngine's
            // own no-valid-OnSuccess-target auto-decline) route, which both funnel through
            // ResolveCurrentEffect(false) -> EffectContext.OnCancelled.
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.IsOptional = true;
            devourEffect.Alternative = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);
            _player.AddToHand(TestData.Cards.CheapCard());

            ChaosWarlords.Source.Core.Contexts.EffectContext? captured = null;
            _context.ActionSystem
                .When(a => a.PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>()))
                .Do(call => captured ??= call.Arg<ChaosWarlords.Source.Core.Contexts.EffectContext>());

            // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true, Tests.Utilities.TestLogger.Instance);
            Assert.IsNotNull(captured);
            captured!.OnCancelled?.Invoke(); // Simulate decline

            // Assert: a second PushEffect call for the Alternative
            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(
                c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.GainResource));
        }

        [TestMethod]
        public void ResolveEffects_WithAlternative_AcceptedViaCallback_DoesNotPushAlternative()
        {
            // Arrange: same shape as above, but simulates accepting instead - proves mutual
            // exclusivity (the original Wight/Cultist of Myrkul bug was both firing).
            var card = TestData.Cards.DevourCard();
            var devourEffect = card.Effects.Find(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect);
            devourEffect!.IsOptional = true;
            devourEffect.Alternative = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);
            _player.AddToHand(TestData.Cards.CheapCard());

            ChaosWarlords.Source.Core.Contexts.EffectContext? captured = null;
            _context.ActionSystem
                .When(a => a.PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>()))
                .Do(call => captured ??= call.Arg<ChaosWarlords.Source.Core.Contexts.EffectContext>());

            // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true, Tests.Utilities.TestLogger.Instance);
            Assert.IsNotNull(captured);
            captured!.OnResolved.Invoke(true); // Simulate accept (no OnSuccess here, so no further push)

            // Assert: still only the one initial push - the Alternative never fired
            _context.ActionSystem.Received(1).PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        #endregion

        #region PushEffectContext Dynamic Repeat Count Tests (Quaggoth, tested via ResolveEffects)

        private static Site MakeSite(string name) => new NonCitySite(name, ResourceType.Power, 1, ResourceType.Power, 1);

        [TestMethod]
        public void ResolveEffects_Assassinate_DynamicAmountSourceSitesControlled_SetsRemainingRepeatsToSiteCount()
        {
            // Quaggoth: "Assassinate one white troop for each site you control" - the repeat
            // COUNT itself (not a resource amount) comes from DynamicAmountSource this time.
            var siteA = MakeSite("A");
            siteA.Owner = _player.Color;
            var siteB = MakeSite("B");
            siteB.Owner = _player.Color;
            var siteC = MakeSite("C"); // Not controlled by _player - must not count.
            _context.MapManager.Sites.Returns(new List<Site> { siteA, siteB, siteC });
            _context.MapManager.HasValidAssassinationTarget(Arg.Any<Player>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<Site>()).Returns(true);

            var card = new Card("test-quaggoth", "Test Quaggoth", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Assassinate, 0, ResourceType.None)
            {
                TargetNeutralTroopOnly = true,
                DynamicAmountSource = DynamicAmountSource.SitesControlled
            };
            card.AddEffect(effect);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.RemainingRepeats == 2));
        }

        [TestMethod]
        public void ResolveEffects_Assassinate_DynamicAmountSourceResolvesToZero_SkipsTheWholeEffectEntirely()
        {
            // Controlling 0 sites must skip Assassinate entirely - NOT fall back to the
            // Math.Max(1, ...) floor every other repeat-capable effect gets, which would force a
            // phantom single Assassinate despite the card's own "for each" text meaning zero.
            _context.MapManager.Sites.Returns(new List<Site>());
            _context.MapManager.HasValidAssassinationTarget(Arg.Any<Player>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<Site>()).Returns(true);

            var card = new Card("test-quaggoth", "Test Quaggoth", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Assassinate, 0, ResourceType.None)
            {
                TargetNeutralTroopOnly = true,
                DynamicAmountSource = DynamicAmountSource.SitesControlled
            };
            card.AddEffect(effect);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Any<ChaosWarlords.Source.Core.Contexts.EffectContext>());
        }

        [TestMethod]
        public void ResolveEffects_Assassinate_DynamicAmountSourceResolvesToZeroWithAlternative_PushesTheAlternativeInstead()
        {
            _context.MapManager.Sites.Returns(new List<Site>());
            _context.MapManager.HasValidAssassinationTarget(Arg.Any<Player>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<Site>()).Returns(true);

            var card = new Card("test-quaggoth", "Test Quaggoth", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Assassinate, 0, ResourceType.None)
            {
                TargetNeutralTroopOnly = true,
                DynamicAmountSource = DynamicAmountSource.SitesControlled,
                Alternative = new CardEffect(EffectType.GainResource, 1, ResourceType.Influence)
            };
            card.AddEffect(effect);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.Assassinate));
            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.SourceEffect != null && c.SourceEffect.Type == EffectType.GainResource));
        }

        [TestMethod]
        public void ResolveEffects_Assassinate_WithoutDynamicAmountSource_StillUsesTheFixedAmountFloor()
        {
            // Regression guard: a plain repeat-capable effect (no DynamicAmountSource at all,
            // e.g. Deathblade's "Assassinate 2 troops") must be completely unaffected by this
            // new gate.
            _context.MapManager.HasValidAssassinationTarget(Arg.Any<Player>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<Site>()).Returns(true);

            var card = new Card("test-deathblade", "Test Deathblade", 1, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.Assassinate, 2, ResourceType.None);
            card.AddEffect(effect);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).PushEffect(Arg.Is<ChaosWarlords.Source.Core.Contexts.EffectContext>(c => c.RemainingRepeats == 2));
        }

        #endregion
    }
}
