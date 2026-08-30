using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Actions
{
    [TestClass]
    [TestCategory("Unit")]
    public class ActionSystemDevourChainTests
    {
        private ActionSystem _actionSystem = null!;
        private ITurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMatchManager _matchManager = null!;

        [TestInitialize]
        public void Setup()
        {
            Utilities.TestLogger.Initialize();
            _turnManager = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            _matchManager = Substitute.For<IMatchManager>();

            _actionSystem = new ActionSystem(_turnManager, _mapManager, Utilities.TestLogger.Instance);
            _actionSystem.SetMatchManager(_matchManager);
        }

        [TestMethod]
        public void AdvanceDevourChain_Wight_TransitionsToSupplant()
        {
            // Arrange
            // Wight: Devour -> OnSuccess: Supplant
            var wight = new Card("wight", "Wight", 3, CardAspect.Sorcery, 1, 1, 0);

            // We simulate the Stack behavior manually for Unit Testing
            // In reality, CardEffectProcessor would wire this up.

            // 2. The Child Effect (Supplant) to be pushed when Devour resolves
            var supplantCtx = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingSupplant,
                wight,
                true, // Requires Input
                "Supplant",
                (s) => { }
            );

            // 1. The Parent Effect (Devour)
            var devourCtx = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingDevourHand,
                wight,
                true, // Requires Input
                "Devour",
                (success) =>
                {
                    if (success) _actionSystem.PushEffect(supplantCtx);
                }
            );

            // Push Parent
            _actionSystem.PushEffect(devourCtx);

            // Act 1: Start Processing (Enters Devour State)
            _actionSystem.ProcessStack();
            Assert.AreEqual(ActionState.TargetingDevourHand, _actionSystem.CurrentState, "Should be in Devour state");

            // Act 2: Complete the Devour Action (Simulate Command Execution)
            // This pops Devour, runs OnResolved (Pushing Supplant), and processes next.
            _actionSystem.ResolveCurrentEffect(true);

            // Assert
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState, "Should transition to Supplant state");
        }

        [TestMethod]
        public void AdvanceDevourChain_Corruptor_FinishesPlay()
        {
            // Arrange
            // Corruptor: Devour -> OnSuccess: GainResource (Non-Blocking)
            var corruptor = new Card("corruptor", "Corruptor", 3, CardAspect.Sorcery, 1, 1, 0);

            // 2. Child: Gain Resource (Auto)
            var resourceCtx = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.Normal, // Non-blocking effects usually don't set a specific state or clear it? 
                                    // Actually ActionSystem.ProcessStack handles requiresInput=false by executing immediately.
                corruptor,
                false, // Auto
                "Gain Resource",
                (s) =>
                {
                    // Verify this runs
                    _matchManager.ResumeDevourChain(corruptor); // Mock call for verification
                }
            );

            // 1. Parent: Devour
            var devourCtx = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingDevourMarket,
                corruptor,
                true,
                "Devour",
                (success) =>
                {
                    if (success) _actionSystem.PushEffect(resourceCtx);
                }
            );

            _actionSystem.PushEffect(devourCtx);

            // Act 1: Process (Enter Devour)
            _actionSystem.ProcessStack();
            Assert.AreEqual(ActionState.TargetingDevourMarket, _actionSystem.CurrentState);

            // Act 2: Resolve Devour
            _actionSystem.ResolveCurrentEffect(true);

            // Stack behavior:
            // 1. Devour Pops.
            // 2. OnResolved Pushes Resource.
            // 3. ProcessStack runs Resource (Auto).
            // 4. Resource Resolves.
            // 5. Stack Empty -> ClearState (Normal).

            // Assert
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "Should clear state after auto-effect");
            _matchManager.Received(1).ResumeDevourChain(corruptor);
        }

        [TestMethod]
        public void AdvanceDevourChain_FromInnerCircleState_TransitionsToNextEffect()
        {
            // Arrange
            var cultist = new Card("cultist", "Cultist", 3, CardAspect.Sorcery, 1, 1, 0);

            var resourceCtx = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.Normal,
                cultist,
                false,
                "Gain Resource",
                (s) => { _matchManager.ResumeDevourChain(cultist); }
            );

            var devourCtx = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingDevourInnerCircle,
                cultist,
                true,
                "Devour",
                (success) => { if (success) _actionSystem.PushEffect(resourceCtx); }
            );

            _actionSystem.PushEffect(devourCtx);
            _actionSystem.ProcessStack();

            // Act: Resolve
            _actionSystem.ResolveCurrentEffect(true);

            // Assert
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "Should transition to Normal (Next effect is instant)");
            _matchManager.Received(1).ResumeDevourChain(cultist);
        }

        // --- PerformAssassinate/PerformSupplant devour consumption ---
        //
        // These two methods are the intended consumer of a devour deferred earlier in a
        // chained effect (e.g. a card reading "Devour a card in your hand -> Supplant a
        // troop"). No shipped card currently reaches them via a live click (see
        // planning.txt RESOLVED, 2026-08-30, for the full trace of why: the live
        // optional-effect-accept path in ActionSystem hardcodes its devour as immediate,
        // not deferred) - but the pre-target/replay flow (TryExecuteSupplantPreTarget) and
        // ActionSystem.DeferDevour both do exercise this path, and it's exactly the kind
        // of latent code a future card or a defer-flow fix would land on. Pinning down its
        // behavior here guards against it silently regressing again.

        [TestMethod]
        public void PerformSupplant_WithDevourCardId_DevoursCard_AndClearsPendingDevourCard()
        {
            // Arrange
            var player = new Player(PlayerColor.Red);
            var cardToDevour = new Card("devour_me", "Devour Me", 3, CardAspect.Sorcery, 1, 1, 0);
            player.AddToHand(cardToDevour);
            _turnManager.ActivePlayer.Returns(player);

            var node = new MapNode(1, new Vector2(0, 0));

            // Act
            _actionSystem.PerformSupplant(node, cardId: "wight", devourCardId: "devour_me");

            // Assert
            _matchManager.Received(1).DevourCard(cardToDevour);
            _mapManager.Received(1).Supplant(node, player);
            Assert.IsNull(_actionSystem.PendingDevourCard, "Pending devour should be cleared once consumed, so it can't leak into a later action.");
        }

        [TestMethod]
        public void PerformAssassinate_WithDevourCardId_DevoursCard_AndClearsPendingDevourCard()
        {
            // Arrange
            var player = new Player(PlayerColor.Red);
            var cardToDevour = new Card("devour_me", "Devour Me", 3, CardAspect.Sorcery, 1, 1, 0);
            player.AddToHand(cardToDevour);
            _turnManager.ActivePlayer.Returns(player);

            var node = new MapNode(1, new Vector2(0, 0));

            // Act
            _actionSystem.PerformAssassinate(node, cardId: "some_card", devourCardId: "devour_me");

            // Assert
            _matchManager.Received(1).DevourCard(cardToDevour);
            _mapManager.Received(1).Assassinate(node, player);
            Assert.IsNull(_actionSystem.PendingDevourCard, "Pending devour should be cleared once consumed, so it can't leak into a later action.");
        }

        [TestMethod]
        public void PerformSupplant_FallsBackToPendingDevourCard_WhenNoDevourCardIdProvided()
        {
            // Arrange: simulates the deferred flow (ActionSystem.DeferDevour), where the
            // card to devour is buffered on PendingDevourCard rather than threaded through
            // as an explicit id.
            var player = new Player(PlayerColor.Red);
            var cardToDevour = new Card("buffered", "Buffered", 3, CardAspect.Sorcery, 1, 1, 0);
            player.AddToHand(cardToDevour);
            _turnManager.ActivePlayer.Returns(player);

            _actionSystem.DeferDevour(cardToDevour);
            Assert.AreEqual(cardToDevour, _actionSystem.PendingDevourCard, "Sanity check: devour should be buffered before Perform* runs.");

            var node = new MapNode(1, new Vector2(0, 0));

            // Act
            _actionSystem.PerformSupplant(node, cardId: null, devourCardId: null);

            // Assert
            _matchManager.Received(1).DevourCard(cardToDevour);
            Assert.IsNull(_actionSystem.PendingDevourCard, "Pending devour should be cleared once consumed.");
        }

        [TestMethod]
        public void PerformSupplant_WithNoDevourPending_DoesNotCallDevourCard()
        {
            // Arrange: the ordinary case - a plain Supplant with nothing deferred.
            var player = new Player(PlayerColor.Red);
            _turnManager.ActivePlayer.Returns(player);

            var node = new MapNode(1, new Vector2(0, 0));

            // Act
            _actionSystem.PerformSupplant(node, cardId: "wight", devourCardId: null);

            // Assert
            _matchManager.DidNotReceiveWithAnyArgs().DevourCard(default!);
        }
    }
}
