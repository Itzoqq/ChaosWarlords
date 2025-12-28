using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Source.Systems
{
    [TestClass]
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
            _player = new Player(PlayerColor.Red);

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
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.GainResource, 3) { TargetResource = ResourceType.Power });

            _processor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_SkippedWithoutFocus()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.GainResource, 3) { TargetResource = ResourceType.Power, RequiresFocus = true });

            _processor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(0, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_RequiresFocus_AppliedWithFocus()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.GainResource, 3) { TargetResource = ResourceType.Power, RequiresFocus = true });

            _processor.ResolveEffects(card, _context, hasFocus: true);

            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void ResolveEffects_Promote_AddsCreditToTurnContext()
        {
            var card = new Card("t", "t", 0, CardAspect.Blasphemy, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Promote, 1));

            _processor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void ResolveEffects_GainInfluence_AddsToPlayer()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.GainResource, 5) { TargetResource = ResourceType.Influence });

            _processor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(5, _player.Influence);
        }

        [TestMethod]
        public void ResolveEffects_DrawCard_CallsPlayerDrawCards()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.DrawCard, 2));

            // Add cards to deck so DrawCards can work
            _player.DeckManager.AddToTop(new Card("c1", "c1", 0, CardAspect.Warlord, 0, 0, 0));
            _player.DeckManager.AddToTop(new Card("c2", "c2", 0, CardAspect.Warlord, 0, 0, 0));

            _processor.ResolveEffects(card, _context, hasFocus: false);

            Assert.HasCount(2, _player.Hand);
        }

        [TestMethod]
        public void ResolveEffects_MoveUnit_WithValidTargets_StartsTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.MoveUnit, 1));

            _context.MapManager.HasValidMoveSource(_player).Returns(true);

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingMoveSource, card);
        }

        [TestMethod]
        public void ResolveEffects_MoveUnit_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.MoveUnit, 1));

            _context.MapManager.HasValidMoveSource(_player).Returns(false);

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Assassinate_WithValidTargets_StartsTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Assassinate, 1));

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingAssassinate, card);
        }

        [TestMethod]
        public void ResolveEffects_Assassinate_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Assassinate, 1));

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(false);

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Supplant_WithValidTargetsAndTroops_StartsTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Supplant, 1));

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);
            _player.TroopsInBarracks = 1;

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingSupplant, card);
        }

        [TestMethod]
        public void ResolveEffects_Supplant_NoTroops_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Supplant, 1));

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(true);
            _player.TroopsInBarracks = 0;

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Supplant_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Supplant, 1));

            _context.MapManager.HasValidAssassinationTarget(_player).Returns(false);
            _player.TroopsInBarracks = 1;

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_WithValidTargetsAndSpies_StartsTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.PlaceSpy, 1));

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 1;

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingPlaceSpy, card);
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoSpies_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.PlaceSpy, 1));

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(true);
            _player.SpiesInBarracks = 0;

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_PlaceSpy_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.PlaceSpy, 1));

            _context.MapManager.HasValidPlaceSpyTarget(_player).Returns(false);
            _player.SpiesInBarracks = 1;

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_ReturnUnit_WithValidTargets_StartsTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.ReturnUnit, 1));

            _context.MapManager.HasValidReturnTroopTarget(_player).Returns(true);

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).StartTargeting(ActionState.TargetingReturn, card);
        }

        [TestMethod]
        public void ResolveEffects_ReturnUnit_NoValidTargets_DoesNotStartTargeting()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.ReturnUnit, 1));

            _context.MapManager.HasValidReturnTroopTarget(_player).Returns(false);

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_Devour_WithCardsInHand_StartsDevour()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Devour, 1));

            _player.Hand.Add(new Card("h1", "h1", 0, CardAspect.Warlord, 0, 0, 0));

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.Received(1).TryStartDevourHand(card);
        }

        [TestMethod]
        public void ResolveEffects_Devour_EmptyHand_DoesNotStartDevour()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.Devour, 1));

            // Hand is empty by default

            _processor.ResolveEffects(card, _context, hasFocus: false);

            _context.ActionSystem.DidNotReceive().TryStartDevourHand(Arg.Any<Card>());
        }

        [TestMethod]
        public void ResolveEffects_MultipleEffects_AllApplied()
        {
            var card = new Card("t", "t", 0, CardAspect.Warlord, 0, 0, 0);
            card.Effects.Add(new CardEffect(EffectType.GainResource, 2) { TargetResource = ResourceType.Power });
            card.Effects.Add(new CardEffect(EffectType.GainResource, 3) { TargetResource = ResourceType.Influence });
            card.Effects.Add(new CardEffect(EffectType.Promote, 1));

            _processor.ResolveEffects(card, _context, hasFocus: false);

            Assert.AreEqual(2, _player.Power);
            Assert.AreEqual(3, _player.Influence);
            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }
    }
}


