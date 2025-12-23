using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

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
                Substitute.For<ICardDatabase>()
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
    }
}