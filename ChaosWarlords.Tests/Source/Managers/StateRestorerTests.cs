using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Tests.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Managers
{
    /// <summary>
    /// StateRestorer had zero test coverage before this file, despite being the mechanism
    /// CommandDispatcher relies on to roll a MatchContext back to a pre-command snapshot when
    /// a command throws mid-execution (see CommandDispatcher.Dispatch). A silent restoration
    /// bug here would leave the game in a state that looks fine but has quietly diverged from
    /// what was actually recorded/replayed - exactly the failure mode multiplayer/replay
    /// depends on never happening. CommandDispatcherTests.Dispatch_WhenExecutionFails_DoesNotRecord
    /// only checks that the failed command wasn't recorded to replay - it does not check that
    /// anything was actually restored.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class StateRestorerTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private Dictionary<string, Card> _cardsById = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private MapNode _node = null!;

        [TestInitialize]
        public void Setup()
        {
            TestLogger.Initialize();
            var logger = TestLogger.Instance;

            _player = new Player(PlayerColor.Red, displayName: "Player 1");
            _player.SeatIndex = 0;

            var turnManager = new TurnManager(
                new List<Player> { _player },
                new SeededGameRandom(12345, logger),
                logger);

            _mapManager = Substitute.For<IMapManager>();
            _node = new MapNode(1, new ChaosWarlords.Source.Core.Data.LogicVector2(0, 0));
            _mapManager.Nodes.Returns(new List<MapNode> { _node });
            _mapManager.Sites.Returns(new List<Site>());

            _marketManager = Substitute.For<IMarketManager>();
            _marketManager.MarketRow.Returns(new List<Card>());

            var cardDb = Substitute.For<ICardDatabase>();
            _cardsById = new Dictionary<string, Card>();
            cardDb.GetCardById(Arg.Any<string>(), Arg.Any<IGameRandom?>())
                .Returns(ci => _cardsById.TryGetValue((string)ci[0], out var c) ? c : null);

            var playerState = new PlayerStateManager(logger);
            var actionSystem = new ActionSystem(turnManager, _mapManager, logger, playerState, _marketManager);

            _context = new MatchContext(turnManager, _mapManager, _marketManager, actionSystem, cardDb, playerState, logger, seed: 999);
            actionSystem.SetMatchContext(_context);

            var matchManager = new MatchManager(_context, logger, Substitute.For<IVictoryManager>());
            actionSystem.SetMatchManager(matchManager);
        }

        private Card RegisterCard(string id, CardLocation location = CardLocation.Deck)
        {
            var card = new Card(id, id, 1, CardAspect.Neutral, 0, 0, 0) { Location = location };
            _cardsById[id] = card;
            return card;
        }

        [TestMethod]
        public void RestoreState_RevertsMetaState()
        {
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _context.CurrentTurnNumber = 99;
            _context.SequenceNumber = 42;

            StateRestorer.RestoreState(_context, snapshot);

            Assert.AreEqual(0, _context.CurrentTurnNumber);
            Assert.AreEqual(0, _context.SequenceNumber);
        }

        [TestMethod]
        public void RestoreState_RevertsPlayerResources()
        {
            _player.AddPower(3);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            // Mutate further, as a failing command would.
            _player.AddPower(10);
            _player.SetInfluence(7);
            _player.VictoryPoints = 5;
            _player.TroopsInBarracks = 1;
            _player.SpiesInBarracks = 0;

            StateRestorer.RestoreState(_context, snapshot);

            Assert.AreEqual(3, _player.Power, "Power should revert to its pre-mutation snapshot value.");
            Assert.AreEqual(0, _player.Influence);
            Assert.AreEqual(0, _player.VictoryPoints);
            Assert.AreEqual(GameConstants.StartingTroops, _player.TroopsInBarracks);
            Assert.AreEqual(GameConstants.StartingSpies, _player.SpiesInBarracks);
        }

        [TestMethod]
        public void RestoreState_RevertsPlayerHand()
        {
            var keptCard = RegisterCard("kept", CardLocation.Hand);
            _player.AddToHand(keptCard);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            // Simulate a command that devoured the kept card and drew a new one before failing.
            _player.RemoveFromHand(keptCard);
            var extraCard = RegisterCard("extra", CardLocation.Hand);
            _player.AddToHand(extraCard);

            StateRestorer.RestoreState(_context, snapshot);

            CollectionAssert.Contains(_player.Hand.ToList(), keptCard, "The pre-mutation hand card must be restored.");
            Assert.HasCount(1, _player.Hand, "The card added after the snapshot must NOT survive the rollback.");
        }

        [TestMethod]
        public void RestoreState_RevertsMapNodeOccupant()
        {
            _node.Occupant = PlayerColor.None;
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _node.Occupant = PlayerColor.Red;

            StateRestorer.RestoreState(_context, snapshot);

            Assert.AreEqual(PlayerColor.None, _node.Occupant);
        }

        [TestMethod]
        public void RestoreState_RevertsVoidPile()
        {
            var snapshot = DtoMapper.ToGameStateDto(_context);
            Assert.IsEmpty(_context.VoidPile);

            var devoured = RegisterCard("devoured", CardLocation.Void);
            _context.VoidPile.Add(devoured);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.IsEmpty(_context.VoidPile, "A card added to VoidPile after the snapshot must not survive rollback.");
        }

        [TestMethod]
        public void RestoreState_RevertsCardsMarkedForTurnEndDevour()
        {
            var snapshot = DtoMapper.ToGameStateDto(_context);
            Assert.IsEmpty(_context.CardsMarkedForTurnEndDevour);

            var marked = RegisterCard("marked_for_devour", CardLocation.Played);
            _context.CardsMarkedForTurnEndDevour.Add(marked);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.IsEmpty(_context.CardsMarkedForTurnEndDevour, "A self-devour mark added after the snapshot must not survive rollback.");
        }

        [TestMethod]
        public void RestoreState_RevertsPendingOpponentDiscardTriggers()
        {
            // Same shape as RestoreState_RevertsCardsMarkedForTurnEndDevour - Neogi's
            // "each opponent discards at end of turn" trigger list needs the same rollback
            // safety (see MatchManager.EndTurn's opponent-discard phase / planning.txt).
            var snapshot = DtoMapper.ToGameStateDto(_context);
            Assert.IsEmpty(_context.PendingOpponentDiscardTriggers);

            var neogi = RegisterCard("neogi", CardLocation.Played);
            _context.PendingOpponentDiscardTriggers.Add(neogi);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.IsEmpty(_context.PendingOpponentDiscardTriggers, "A Neogi-style discard trigger added after the snapshot must not survive rollback.");
        }

        [TestMethod]
        public void RestoreState_RevertsActionSystemCurrentStateAndPendingMoveSource()
        {
            // Real API, not RestorePendingState directly - exercises the same path a genuine
            // in-progress Move Troop targeting sequence would.
            _context.ActionSystem.SetMoveSource(_node);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            // Mutate further, as a failing command that changed targeting state would.
            _context.ActionSystem.RestorePendingState(ActionState.Normal, null, null, null, null);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.AreEqual(ActionState.TargetingMoveDestination, _context.ActionSystem.CurrentState);
            Assert.AreSame(_node, _context.ActionSystem.PendingMoveSource,
                "Restored PendingMoveSource should resolve to the SAME MapNode instance MapManager " +
                "already holds - RestoreMap mutates nodes in place rather than recreating them.");
        }

        [TestMethod]
        public void RestoreState_RevertsActionSystemPendingSite()
        {
            var site = new NonCitySite("Test Site", ResourceType.Power, 0, ResourceType.Power, 0) { Id = 7 };
            _mapManager.Sites.Returns(new List<Site> { site });

            _context.ActionSystem.TransitionToSpySelection(site);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _context.ActionSystem.RestorePendingState(ActionState.Normal, null, null, null, null);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.AreEqual(ActionState.SelectingSpyToReturn, _context.ActionSystem.CurrentState);
            Assert.AreSame(site, _context.ActionSystem.PendingSite,
                "Restored PendingSite should resolve to the SAME Site instance MapManager already holds.");
        }

        [TestMethod]
        public void RestoreState_RevertsActionSystemPendingCardAndPendingDevourCard()
        {
            var pendingCard = RegisterCard("wight", CardLocation.Hand);
            var devourCard = RegisterCard("victim", CardLocation.Hand);

            // No side-effect-free public API sets PendingCard/PendingDevourCard in isolation
            // (they're set as part of larger targeting/devour flows) - RestorePendingState is
            // the documented restore-only entry point, so using it to arrange the "before"
            // state here exercises exactly what StateRestorer itself calls, just earlier.
            _context.ActionSystem.RestorePendingState(ActionState.TargetingAssassinate, pendingCard, null, null, devourCard);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _context.ActionSystem.RestorePendingState(ActionState.Normal, null, null, null, null);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.AreEqual(ActionState.TargetingAssassinate, _context.ActionSystem.CurrentState);
            Assert.AreEqual("wight", _context.ActionSystem.PendingCard?.Id);
            Assert.AreEqual("victim", _context.ActionSystem.PendingDevourCard?.Id);
        }

        [TestMethod]
        public void RestoreState_RevertsMarketRow()
        {
            var marketRow = new List<Card>();
            _marketManager.MarketRow.Returns(marketRow);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            var bought = RegisterCard("bought", CardLocation.Market);
            marketRow.Add(bought);

            StateRestorer.RestoreState(_context, snapshot);

            Assert.IsEmpty(_marketManager.MarketRow, "A card added to the market row after the snapshot must not survive rollback.");
        }

        // --- RestoreEffect coverage (planning.txt TIER 1 item 1 - risk-hotspot remediation:
        // StateRestorer.RestoreEffect was flagged with Crap 42 / cyclomatic 6, a coverage gap
        // (nothing above exercises restoring a genuinely non-empty ExecutionStack) rather than
        // a complexity problem. These construct the snapshot's EffectStack list directly,
        // rather than pushing a real EffectContext first, to isolate each of RestoreEffect's
        // own guard branches. ---

        [TestMethod]
        public void RestoreState_EffectStackEntry_WithNoSourceCardId_IsSkipped()
        {
            var snapshot = DtoMapper.ToGameStateDto(_context);
            snapshot.EffectStack = new List<EffectContextDto>
            {
                new() { State = ActionState.TargetingAssassinate, SourceCardId = null, RequiresInput = true }
            };

            StateRestorer.RestoreState(_context, snapshot);

            Assert.IsEmpty(_context.ActionSystem.ExecutionStack, "An effect entry with no SourceCardId can't be reconstructed - it should be dropped, not throw.");
        }

        [TestMethod]
        public void RestoreState_EffectStackEntry_WhoseSourceCardNoLongerExists_IsSkipped()
        {
            var snapshot = DtoMapper.ToGameStateDto(_context);
            snapshot.EffectStack = new List<EffectContextDto>
            {
                new() { State = ActionState.TargetingAssassinate, SourceCardId = "card_that_does_not_exist", RequiresInput = true }
            };

            StateRestorer.RestoreState(_context, snapshot);

            Assert.IsEmpty(_context.ActionSystem.ExecutionStack, "A SourceCardId the CardDatabase can't resolve should be dropped, not throw.");
        }

        [TestMethod]
        public void RestoreState_EffectStackEntry_WithTargetingState_ReattachesTheMatchingCardEffect()
        {
            var sourceCard = RegisterCard("wight", CardLocation.Played);
            var devourEffect = new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.Hand };
            sourceCard.AddEffect(devourEffect);

            var snapshot = DtoMapper.ToGameStateDto(_context);
            snapshot.EffectStack = new List<EffectContextDto>
            {
                new() { State = ActionState.TargetingDevourHand, SourceCardId = "wight", RequiresInput = true, EffectType = EffectType.Devour }
            };

            StateRestorer.RestoreState(_context, snapshot);

            Assert.HasCount(1, _context.ActionSystem.ExecutionStack);
            var restored = _context.ActionSystem.ExecutionStack.Peek();
            Assert.AreSame(devourEffect, restored.SourceEffect, "A non-Normal state should re-resolve SourceEffect from the card's own Effects, matched by EffectType.");
        }

        [TestMethod]
        public void RestoreState_EffectStackEntry_WithNormalState_LeavesSourceEffectNull()
        {
            // Mirrors ResolveOpponentDiscard-style bookkeeping entries (see GameStateDto.
            // ActionSystemState's own doc comment) - State==Normal is a valid, real shape for
            // an EffectContext, and RestoreEffect deliberately does NOT look up a CardEffect
            // for it (there's nothing to target).
            var sourceCard = RegisterCard("wight", CardLocation.Played);
            sourceCard.AddEffect(new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.Hand });

            var snapshot = DtoMapper.ToGameStateDto(_context);
            snapshot.EffectStack = new List<EffectContextDto>
            {
                new() { State = ActionState.Normal, SourceCardId = "wight", RequiresInput = false, EffectType = EffectType.Devour }
            };

            StateRestorer.RestoreState(_context, snapshot);

            Assert.HasCount(1, _context.ActionSystem.ExecutionStack);
            Assert.IsNull(_context.ActionSystem.ExecutionStack.Peek().SourceEffect);
        }
    }
}
