using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Tests.Utilities;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// ActionSystemCancellationTests.cs never calls ActionSystem.SetMatchContext, so every
    /// test there exercises CancelTargeting's FALLBACK path (no snapshot available) - none of
    /// them touch the actual new snapshot/restore mechanism at all (see ActionSystem.
    /// CancelTargeting's own doc comment and planning.txt). This file wires a real
    /// MatchContext (mirroring StateRestorerTests.cs's setup) so these tests exercise the real
    /// path: a full-state snapshot taken at StartTargeting, restored on CancelTargeting.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ActionSystemCancelTargetingSnapshotTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private ActionSystem _actionSystem = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private PlayerStateManager _playerStateManager = null!;
        private IGameLogger _logger = null!;
        private Dictionary<string, Card> _cardsById = null!;

        [TestInitialize]
        public void Setup()
        {
            TestLogger.Initialize();
            _logger = TestLogger.Instance;

            _player = new Player(PlayerColor.Red, displayName: "Player 1");
            _player.SeatIndex = 0;

            var turnManager = new TurnManager(
                new List<Player> { _player },
                new SeededGameRandom(20260901, _logger),
                _logger);

            _mapManager = Substitute.For<IMapManager>();
            _mapManager.Nodes.Returns(new List<MapNode>());
            _mapManager.Sites.Returns(new List<Site>());

            _marketManager = Substitute.For<IMarketManager>();
            _marketManager.MarketRow.Returns(new List<Card>());

            // A snapshot restore re-resolves every card by DefinitionId via
            // CardDatabase.GetCardById (see StateRestorer's own doc comments on Card identity
            // across a restore) - route it to a real lookup table, not a bare null-returning
            // mock, or every card silently vanishes from the restored Hand/PlayedCards
            // collections instead of round-tripping. Mirrors StateRestorerTests.cs's own
            // _cardsById pattern.
            _cardsById = new Dictionary<string, Card>();
            var cardDb = Substitute.For<ICardDatabase>();
            cardDb.GetCardById(Arg.Any<string>(), Arg.Any<IGameRandom?>())
                .Returns(ci => _cardsById.TryGetValue((string)ci[0], out var c) ? c : null);

            _actionSystem = new ActionSystem(turnManager, _mapManager, _logger);
            _playerStateManager = new PlayerStateManager(_logger);
            _actionSystem.SetPlayerStateManager(_playerStateManager);
            _actionSystem.SetMarketManager(_marketManager);

            _context = new MatchContext(turnManager, _mapManager, _marketManager, _actionSystem, cardDb, _playerStateManager, _logger, seed: 20260901);

            // The one line ActionSystemCancellationTests.cs's Setup() omits - wiring this is
            // what makes StartTargeting actually take a snapshot instead of silently no-op'ing.
            _actionSystem.SetMatchContext(_context);

            var matchManager = new MatchManager(_context, _logger, Substitute.For<IVictoryManager>());
            _actionSystem.SetMatchManager(matchManager);
        }

        private Card RegisterCard(string id, CardLocation location = CardLocation.Deck)
        {
            var card = new Card(id, id, 0, CardAspect.Neutral, 0, 0, 0) { Location = location };
            _cardsById[id] = card;
            return card;
        }

        [TestMethod]
        public void CancelTargeting_WithMatchContextWired_StillReturnsPlayedCardToHand()
        {
            // Same scenario as ActionSystemCancellationTests.
            // CancelTargeting_WithPendingCardInPlayedPile_ReturnsCardToHand, but this time
            // through the real snapshot/restore path - proving TryRestoreCardToHand's
            // ID-based post-restore lookup actually works, not just the no-snapshot fallback.
            // Asserted by card ID, not object reference: the snapshot restore replaces the
            // player's Hand/PlayedCards collections with freshly-resolved Card instances (see
            // StateRestorer's own doc comments), so the post-cancel card is a different
            // object than the one this test constructed, with the same Id.
            var card = RegisterCard("test_card", CardLocation.Played);
            _player.AddToPlayed(card);

            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            _actionSystem.CancelTargeting();

            Assert.Contains("test_card", _player.Hand.Select(c => c.Id).ToList(), "Card should be returned to Hand.");
            Assert.DoesNotContain("test_card", _player.PlayedCards.Select(c => c.Id).ToList(), "Card should be removed from PlayedCards.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void CancelTargeting_RevertsResourceSpentDuringTargeting_NotJustTheCard()
        {
            // The actual payoff of the snapshot mechanism, and something
            // TryRestoreCardToHand alone could NEVER do: a resource spent AFTER targeting
            // started but BEFORE it was cancelled reverts too, with no per-mechanic undo code
            // needed anywhere - exactly the "imperative undo trap" planning.txt described.
            _playerStateManager.AddPower(_player, 5);
            int powerBeforeTargeting = _player.Power;

            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Simulate some mechanic spending Power as a side effect of entering/progressing
            // through targeting - the exact class of mutation the old field-by-field
            // CancelTargeting had no general mechanism to undo.
            _playerStateManager.TrySpendPower(_player, 3);
            Assert.AreEqual(powerBeforeTargeting - 3, _player.Power, "Setup check: Power should actually have been spent.");

            _actionSystem.CancelTargeting();

            Assert.AreEqual(powerBeforeTargeting, _player.Power, "Power spent during targeting must be reverted by cancellation.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void CancelTargeting_DuringMultiStepChain_RevertsToBeforeTheWholeSequence_NotJustTheCurrentStep()
        {
            // AdvancePreCommitTargeting calls StartTargeting again for each step of a
            // multi-step card (e.g. Wight's Devour -> Supplant). Re-snapshotting on every
            // step would only let a cancel at step 2 undo back to "just before step 2" - a
            // regression from today's actual behavior (cancelling ANY step undoes the whole
            // play attempt). Confirm the snapshot is taken once, at the transition out of
            // Normal, and survives a second StartTargeting call for the same sequence.
            _playerStateManager.AddPower(_player, 10);
            int powerBeforeSequence = _player.Power;

            _actionSystem.StartTargeting(ActionState.TargetingDevourHand); // "step 1"
            _playerStateManager.TrySpendPower(_player, 2); // step 1's side effect
            _actionSystem.StartTargeting(ActionState.TargetingSupplant);  // "step 2" - CurrentState is no longer Normal here, so this must NOT re-snapshot
            _playerStateManager.TrySpendPower(_player, 4); // step 2's side effect

            _actionSystem.CancelTargeting();

            Assert.AreEqual(powerBeforeSequence, _player.Power, "Cancelling mid-chain must revert BOTH steps' mutations, not just the latest one.");
        }
    }
}
