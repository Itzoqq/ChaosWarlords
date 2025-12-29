using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class CardPlaySystemTests
    {
        private CardPlaySystem _system = null!;
        private MatchContext _matchContext = null!;
        private IMatchManager _matchManager = null!;
        private IActionSystem _actionSystem = null!;
        private IMapManager _mapManager = null!;
        private Action _targetingCallback = null!;

        [TestInitialize]
        public void Setup()
        {
            // Mocks
            _matchManager = Substitute.For<IMatchManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _mapManager = Substitute.For<IMapManager>();
            _targetingCallback = Substitute.For<Action>();

            // Complex Mock for MatchContext (Logic often accesses properties)
            // We can just create a real MatchContext with mocked deps if possible, or Mock it if it's an interface (it's a class).
            // MatchContext is a data container class. Best to instantiate it with mocks.
            var turnManager = Substitute.For<ITurnManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var cardDb = Substitute.For<ICardDatabase>();

            // Create context with our mocked MapManager and ActionSystem
            var ps = new PlayerStateManager();
            _matchContext = new MatchContext(turnManager, _mapManager, marketManager, _actionSystem, cardDb, ps);

            // Set Active Player manually if needed
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            turnManager.ActivePlayer.Returns(player);

            // System under test
            _system = new CardPlaySystem(_matchContext, _matchManager, _targetingCallback);
        }

        [TestMethod]
        public void PlayCard_WithTargetingEffect_ButNoTargets_SkipsTargeting_AndPlaysCard()
        {
            var card = new CardBuilder()
                .WithName("assassin")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Assassinate, 1)
                .Build();

            // Map says NO targets
            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(false);

            _system.PlayCard(card);

            // Verify: No targeting started
            _actionSystem.DidNotReceiveWithAnyArgs().StartTargeting(default!, default!);
            _targetingCallback.DidNotReceive().Invoke();

            // Verify: Card played
            _matchManager.Received(1).PlayCard(card);
        }

        [TestMethod]
        public void PlayCard_WithTargetingEffect_AndTargetsExist_StartsTargeting()
        {
            var card = new CardBuilder()
                .WithName("assassin")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Assassinate, 1)
                .Build();

            // Map says YES targets
            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(true);

            _system.PlayCard(card);

            // Verify: Targeting started
            _actionSystem.Received(1).StartTargeting(ActionState.TargetingAssassinate, card);
            _targetingCallback.Received(1).Invoke();

            // Verify: Card NOT played yet
            _matchManager.DidNotReceive().PlayCard(card);
        }

        [TestMethod]
        public void PlayCard_WithPromote_DoesNotSwitchToTargeting()
        {
            // Promote is NOT a targeting effect in CardPlaySystem (it's phase based)
            var card = new CardBuilder()
                .WithName("noble")
                .WithCost(3)
                .WithAspect(CardAspect.Blasphemy)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Promote, 1)
                .Build();

            _system.PlayCard(card);

            // Verify: No targeting
            _actionSystem.DidNotReceiveWithAnyArgs().StartTargeting(default!, default!);

            // Verify: Card played
            _matchManager.Received(1).PlayCard(card);
        }

        [TestMethod]
        public void PlayCard_Devour_SwitchesToTargeting_WhenHandHasCards()
        {
            var card = new CardBuilder()
                .WithName("devourer")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Devour, 1)
                .Build();

            var player = _matchContext.ActivePlayer;
            player.Hand.Add(card);
            player.Hand.Add(new CardBuilder().WithName("food").WithAspect(CardAspect.Neutral).Build());

            _system.PlayCard(card);

            _actionSystem.Received(1).StartTargeting(ActionState.TargetingDevourHand, card);
            _targetingCallback.Received(1).Invoke();
        }

        [TestMethod]
        public void PlayCard_Devour_SkipsTargeting_WhenHandEmpty()
        {
            var card = new CardBuilder()
                .WithName("devourer")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Devour, 1)
                .Build();

            _matchContext.ActivePlayer.Hand.Clear();
            // (Logic check: GameplayState tests assumed if Hand.Count > 0. If we pass empty hand here...)

            _system.PlayCard(card);

            // Should play immediately
            _matchManager.Received(1).PlayCard(card);
        }

        [TestMethod]
        public void HasViableTargets_ReturnsTrue_WhenTargetsExist()
        {
            var card = new CardBuilder()
                .WithName("assassin")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Assassinate, 1)
                .Build();

            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(true);

            Assert.IsTrue(_system.HasViableTargets(card));
        }

        [TestMethod]
        public void HasViableTargets_ReturnsFalse_WhenNoTargets()
        {
            var card = new CardBuilder()
                .WithName("assassin")
                .WithCost(3)
                .WithAspect(CardAspect.Shadow)
                .WithPower(1)
                .WithInfluence(1)
                .WithEffect(EffectType.Assassinate, 1)
                .Build();

            _mapManager.HasValidAssassinationTarget(Arg.Any<Player>()).Returns(false);

            Assert.IsFalse(_system.HasViableTargets(card));
        }
    }
}



