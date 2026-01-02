using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Source.Systems
{
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
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _processor = new CardEffectProcessor();
            _player = TestData.Players.PoorPlayer();

            var turnSub = Substitute.For<ITurnManager>();
            turnSub.ActivePlayer.Returns(_player);
            // We need a real TurnContext for promotions
            turnSub.CurrentTurnContext.Returns(new TurnContext(_player, ChaosWarlords.Tests.Utilities.TestLogger.Instance));

            _context = new MatchContext(
                turnSub,
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                new PlayerStateManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance), // <--- Use real StateManager for logic testing
                ChaosWarlords.Tests.Utilities.TestLogger.Instance
            );
        }

        [TestMethod]
        public void ResolveEffects_GainPower_AddsToPlayer()
        {
            var card = TestData.Cards.PowerCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_SkippedWithoutFocus()
        {
            var card = TestData.Cards.FocusPowerCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(0, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_AppliedWithFocus()
        {
            var card = TestData.Cards.FocusPowerCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_Promote_AddsCreditToTurnContext()
        {
            var card = TestData.Cards.NobleCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void ResolveEffects_GainInfluence_AddsToPlayer()
        {
            // Note: InfluenceCard() in TestData gives 2 Influence, but the test expected 5.
            // I'll stick to the test's expectation of 5 to keep logic verification valid, 
            // or I could update the test if 2 is enough. 
            // Better to use builder for specific amounts if TestData doesn't match exactly.
            // But wait, the test architecture.md says use TestData for common scenarios.
            // I'll update the test to use InfluenceCard and expect 2.
            var card = TestData.Cards.InfluenceCard();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(2, _player.Influence);
        }

        [TestMethod]
        public void ResolveEffects_DrawCard_CallsPlayerDrawCards()
        {
            var card = TestData.Cards.DrawCard();

            // Add cards to deck so DrawCards can work
            _player.DeckManager.AddToTop(TestData.Cards.CheapCard());
            _player.DeckManager.AddToTop(TestData.Cards.CheapCard());

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.HasCount(2, _player.Hand);
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

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).StartTargeting(expectedState, card);
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

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        private void SetupValidTargets(string validationMethod, bool hasTargets)
        {
            switch (validationMethod)
            {
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

            // Conditions don't matter to the Processor anymore, it just calls TryStartSupplant
            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);
            _player.TroopsInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).TryStartSupplant(card);
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_WithValidTargetsAndSpies_StartsTargeting()
        {
            var card = TestData.Cards.PlaceSpyCard();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingPlaceSpy, card);
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoSpies_DoesNotStartTargeting()
        {
            var card = TestData.Cards.PlaceSpyCard();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 0;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoValidTargets_DoesNotStartTargeting()
        {
            var card = TestData.Cards.PlaceSpyCard();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(false);
            _player.SpiesInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Devour_WithCardsInHand_StartsDevour()
        {
            var card = TestData.Cards.DevourCard();

            _player.Hand.Add(TestData.Cards.CheapCard());

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.Received(1).TryStartDevourHand(card);
        }

        [TestMethod]
        public void ResolveEffects_Devour_EmptyHand_DoesNotStartDevour()
        {
            var card = TestData.Cards.DevourCard();

            // Hand is empty by default

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context.ActionSystem.DidNotReceive().TryStartDevourHand(Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_MultipleEffects_AllApplied()
        {
            // Setup card with multiple effects using TestData for initial card then adding more? 
            // Or just add a MultiEffectCard to TestData.
            var card = TestData.Cards.NobleCard(); // Promote 1
            card.Effects.Add(new CardEffect(EffectType.GainResource, 2, ResourceType.Power));
            card.Effects.Add(new CardEffect(EffectType.GainResource, 3, ResourceType.Influence));

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            Assert.AreEqual(2, _player.Power);
            Assert.AreEqual(3, _player.Influence);
            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }
    }
}


