using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts; // Needed for EffectContext
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// Integration tests for ActionSystem's *completion* path (CompleteAction / the
    /// Perform* methods), the counterpart to ActionSystemCancellationTests (the *abort*
    /// path). That file already thoroughly proves CancelTargeting() always empties
    /// ExecutionStack; this file exists because the completion side had NO equivalent
    /// coverage at all - which is exactly how PerformSupplant's and PerformReturnTroop's
    /// "manual OnActionCompleted+ClearState() instead of CompleteAction()" bug shipped
    /// unnoticed (see planning.txt RESOLVED, 2026-08-31): a manual clear resets
    /// CurrentState but never pops ExecutionStack, so a Perform* method reached via a
    /// chained effect (e.g. a card reading "Devour a card -> Supplant a troop") would
    /// silently leave its EffectContext stuck on the stack forever, to resurface and
    /// force that targeting mode on a totally unrelated later card.
    ///
    /// Every Perform* method on IActionSystem gets one test here: push a dummy blocking
    /// EffectContext (simulating "this action was reached via a chain"), call the method,
    /// assert the stack is empty afterward. This is a fast, isolated way to pin down the
    /// exact invariant the bug violated - CompleteAction()/ResolveCurrentEffect() must be
    /// what resolves the top-of-stack effect, not a parallel ad hoc "clear state" path -
    /// without needing a full card-driven end-to-end scenario per action type the way
    /// WightMechanicsTests does for Supplant specifically.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ActionSystemCompletionTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private ActionSystem _actionSystem = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IMatchManager _matchManager = null!;
        private IGameLogger _logger = null!;
        private PlayerStateManager _playerStateManager = null!;
        private IMarketStateManager _marketStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            Utilities.TestLogger.Initialize();
            _logger = Utilities.TestLogger.Instance;

            _player = new Player(PlayerColor.Red);

            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(_player);

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _matchManager = Substitute.For<IMatchManager>();
            _marketStateManager = Substitute.For<IMarketStateManager>();
            _playerStateManager = new PlayerStateManager(_logger);

            _actionSystem = new ActionSystem(turnManager, _mapManager, _logger);
            _actionSystem.SetMatchManager(_matchManager);
            _actionSystem.SetMarketManager(_marketManager);
            _actionSystem.SetMarketStateManager(_marketStateManager);
            _actionSystem.SetPlayerStateManager(_playerStateManager);

            var cardDb = Substitute.For<ICardDatabase>();
            var uiMediator = Substitute.For<IUIEventMediator>();

            _context = new MatchContext(
                turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                cardDb,
                _playerStateManager,
                uiMediator,
                _logger
            );
            _actionSystem.SetMatchContext(_context);
        }

        /// <summary>
        /// Pushes a dummy blocking EffectContext onto ExecutionStack and enters targeting
        /// state for it via ProcessStack(), simulating "a card's chained effect put us here" -
        /// the exact situation PerformSupplant/PerformReturnTroop mishandled.
        /// </summary>
        private EffectContext PushDummyBlockingEffect(ActionState state, Card sourceCard)
        {
            var effect = new CardEffect(EffectType.GainResource, 1);
            var ctx = new EffectContext(
                state,
                sourceCard,
                true, // Requires Input
                $"Dummy blocking effect for {state}",
                (bool s) => { },
                effect
            );
            _actionSystem.PushEffect(ctx);
            _actionSystem.ProcessStack();
            return ctx;
        }

        [TestMethod]
        public void PerformAssassinate_ReachedViaChain_PopsExecutionStack()
        {
            var card = new Card("source", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            PushDummyBlockingEffect(ActionState.TargetingAssassinate, card);
            var node = TestData.MapNodes.Node1();

            _actionSystem.PerformAssassinate(node, cardId: "paid_by_card");

            Assert.IsEmpty(_actionSystem.ExecutionStack, "PerformAssassinate should pop the chained EffectContext, not leave it stranded.");
        }

        [TestMethod]
        public void PerformSupplant_ReachedViaChain_PopsExecutionStack()
        {
            var card = new Card("source", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            PushDummyBlockingEffect(ActionState.TargetingSupplant, card);
            var node = TestData.MapNodes.Node1();

            _actionSystem.PerformSupplant(node, cardId: "paid_by_card");

            Assert.IsEmpty(_actionSystem.ExecutionStack, "PerformSupplant should pop the chained EffectContext, not leave it stranded (this was the actual bug - see planning.txt RESOLVED).");
        }

        [TestMethod]
        public void PerformReturnTroop_ReachedViaChain_PopsExecutionStack()
        {
            var card = new Card("source", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            PushDummyBlockingEffect(ActionState.TargetingReturn, card);
            var node = TestData.MapNodes.Node1();

            _actionSystem.PerformReturnTroop(node, cardId: "paid_by_card");

            Assert.IsEmpty(_actionSystem.ExecutionStack, "PerformReturnTroop should pop the chained EffectContext, not leave it stranded (same bug shape as PerformSupplant - see planning.txt RESOLVED).");
        }

        [TestMethod]
        public void PerformMoveTroop_ReachedViaChain_PopsExecutionStack()
        {
            var card = new Card("source", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            PushDummyBlockingEffect(ActionState.TargetingMoveDestination, card);
            var source = TestData.MapNodes.Node1();
            var dest = TestData.MapNodes.Node2();

            _actionSystem.PerformMoveTroop(source, dest, cardId: "paid_by_card");

            Assert.IsEmpty(_actionSystem.ExecutionStack, "PerformMoveTroop should pop the chained EffectContext, not leave it stranded.");
        }

        [TestMethod]
        public void PerformPlaceSpy_ReachedViaChain_PopsExecutionStack()
        {
            var card = new Card("source", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            PushDummyBlockingEffect(ActionState.TargetingPlaceSpy, card);
            var site = TestData.Sites.NeutralSite();

            _actionSystem.PerformPlaceSpy(site, cardId: "paid_by_card");

            Assert.IsEmpty(_actionSystem.ExecutionStack, "PerformPlaceSpy should pop the chained EffectContext, not leave it stranded.");
        }

        [TestMethod]
        public void PerformSpyReturn_ReachedViaChain_PopsExecutionStack()
        {
            var card = new Card("source", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            PushDummyBlockingEffect(ActionState.SelectingSpyToReturn, card);
            var site = TestData.Sites.NeutralSite();
            _mapManager.ReturnSpecificSpy(site, _player, PlayerColor.Blue).Returns(true);

            bool result = _actionSystem.PerformSpyReturn(site, PlayerColor.Blue, cardId: "paid_by_card");

            Assert.IsTrue(result, "Sanity check: the underlying spy return should have succeeded.");
            Assert.IsEmpty(_actionSystem.ExecutionStack, "PerformSpyReturn should pop the chained EffectContext, not leave it stranded.");
        }
    }
}
