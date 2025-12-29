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
            GameLogger.Initialize();
            _processor = new CardEffectProcessor();
            _player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();

            var turnSub = Substitute.For<ITurnManager>();
            turnSub.ActivePlayer.Returns(_player);
            // We need a real TurnContext for promotions
            turnSub.CurrentTurnContext.Returns(new TurnContext(_player));

            _context = new MatchContext(
                turnSub,
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                new PlayerStateManager() // <--- Use real StateManager for logic testing
            );
        }

        [TestMethod]
        public void ResolveEffects_GainPower_AddsToPlayer()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.GainResource, 3, ResourceType.Power)
                .Build();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_SkippedWithoutFocus()
        {
            var card = new CardBuilder()
                .WithFocusEffect(EffectType.GainResource, 3, ResourceType.Power)
                .Build();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(0, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_AppliedWithFocus()
        {
            var card = new CardBuilder()
                .WithFocusEffect(EffectType.GainResource, 3, ResourceType.Power)
                .Build();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: true);

            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_Promote_AddsCreditToTurnContext()
        {
            var card = new CardBuilder()
                .WithAspect(CardAspect.Blasphemy)
                .WithEffect(EffectType.Promote, 1)
                .Build();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void ResolveEffects_GainInfluence_AddsToPlayer()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.GainResource, 5, ResourceType.Influence)
                .Build();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(5, _player.Influence);
        }

        [TestMethod]
        public void ResolveEffects_DrawCard_CallsPlayerDrawCards()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.DrawCard, 2)
                .Build();

            // Add cards to deck so DrawCards can work
            _player.DeckManager.AddToTop(new CardBuilder().WithName("c1").Build());
            _player.DeckManager.AddToTop(new CardBuilder().WithName("c2").Build());

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

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
            var card = new CardBuilder()
                .WithEffect(effectType, 1)
                .Build();

            SetupValidTargets(validationMethod, hasTargets: true);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

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
            var card = new CardBuilder()
                .WithEffect(effectType, 1)
                .Build();

            SetupValidTargets(validationMethod, hasTargets: false);

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

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
        public void ResolveEffects_Supplant_WithValidTargetsAndTroops_StartsTargeting()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.Supplant, 1)
                .Build();

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);
            _player.TroopsInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingSupplant, card);
        }

        [TestMethod]
        public void ResolveEffects_Supplant_NoTroops_DoesNotStartTargeting()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.Supplant, 1)
                .Build();

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);
            _player.TroopsInBarracks = 0;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Supplant_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.Supplant, 1)
                .Build();

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(false);
            _player.TroopsInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_WithValidTargetsAndSpies_StartsTargeting()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.PlaceSpy, 1)
                .Build();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingPlaceSpy, card);
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoSpies_DoesNotStartTargeting()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.PlaceSpy, 1)
                .Build();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 0;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.PlaceSpy, 1)
                .Build();

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(false);
            _player.SpiesInBarracks = 1;

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Devour_WithCardsInHand_StartsDevour()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.Devour, 1)
                .Build();

            _player.Hand.Add(new CardBuilder().WithName("h1").Build());

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).TryStartDevourHand(card);
        }

        [TestMethod]
        public void ResolveEffects_Devour_EmptyHand_DoesNotStartDevour()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.Devour, 1)
                .Build();

            // Hand is empty by default

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().TryStartDevourHand(Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_MultipleEffects_AllApplied()
        {
            var card = new CardBuilder()
                .WithEffect(EffectType.GainResource, 2, ResourceType.Power)
                .WithEffect(EffectType.GainResource, 3, ResourceType.Influence)
                .WithEffect(EffectType.Promote, 1)
                .Build();

            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(2, _player.Power);
            Assert.AreEqual(3, _player.Influence);
            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }
    }
}


