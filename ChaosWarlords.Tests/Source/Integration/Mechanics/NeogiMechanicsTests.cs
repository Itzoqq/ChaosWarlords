using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Source.Integration.Mechanics
{
    // Neogi: "Deploy 4 troops. At end of turn, each opponent must discard a card." The
    // cross-player forced-discard sequencing (MatchManager.BeginOpponentDiscardPhase/
    // AdvanceOpponentDiscard/ResolveOpponentDiscard + TurnManager.ForcedActingPlayer) is the
    // largest single piece of new engine work in this batch - see planning.txt.
    [TestClass]
    [TestCategory("Integration")]
    public class NeogiMechanicsTests
    {
        private MatchContext _context = null!;
        private IGameLogger _logger = null!;
        private MapManager _mapManager = null!;
        private ActionSystem _actionSystem = null!;
        private TurnManager _turnManager = null!;
        private Player _red = null!, _blue = null!, _green = null!;
        private IMarketManager _marketManager = null!;
        private MatchManager _matchManager = null!;
        private IPlayerStateManager _playerStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();

            var nodes = new List<MapNode>();
            var sites = new List<Site>();

            _red = new Player(PlayerColor.Red, displayName: "Red");
            _blue = new Player(PlayerColor.Blue, displayName: "Blue");
            _green = new Player(PlayerColor.Black, displayName: "Green"); // 3rd seat, to exercise seat-order sequencing past 2 players

            // Mocked IGameRandom.Shuffle is a no-op (NSubstitute default for an unconfigured
            // void method) - Players stays in exactly the order given below, giving
            // deterministic seat order for these assertions.
            var mockRandom = Substitute.For<IGameRandom>();
            _turnManager = new TurnManager(new List<Player> { _red, _blue, _green }, mockRandom, _logger);

            _playerStateManager = new PlayerStateManager(_logger);
            _mapManager = new MapManager(nodes, sites, _turnManager, _logger, _playerStateManager);
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = new ActionSystem(_turnManager, _mapManager, _logger, _playerStateManager, _marketManager);

            var cardDb = Substitute.For<ICardDatabase>();

            _context = new MatchContext(
                _turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                cardDb,
                _playerStateManager,
                _logger,
                123
            );

            _actionSystem.SetMatchContext(_context);

            var victoryManager = Substitute.For<IVictoryManager>();
            _matchManager = new MatchManager(_context, _logger, victoryManager);
            _actionSystem.SetMatchManager(_matchManager);
        }

        private static Card GetNeogiCard()
        {
            var card = new Card("neogi", "Neogi", 7, CardAspect.Warlord, 4, 8, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 4, ResourceType.Troops));
            card.AddEffect(new CardEffect(EffectType.MarkOpponentDiscardAtEndOfTurn, 1));
            return card;
        }

        private void PlayNeogiAsRed()
        {
            var neogi = GetNeogiCard();
            _red.AddToHand(neogi);
            var playCommand = new PlayCardCommand(neogi);
            playCommand.Execute(_context);
        }

        [TestMethod]
        public void PlayNeogi_MarksOneOpponentDiscardTrigger_DoesNotDiscardYet()
        {
            PlayNeogiAsRed();

            Assert.HasCount(1, _context.PendingOpponentDiscardTriggers);
            Assert.AreEqual(4, _red.PendingFreeTroops, "Deploy 4 troops should apply immediately.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "Playing Neogi itself shouldn't demand anything - the discard is deferred to end of turn.");
            Assert.IsFalse(_matchManager.IsResolvingOpponentDiscard);
        }

        [TestMethod]
        public void EndTurn_AfterNeogi_SequencesOpponentsInSeatOrder_ThenCompletesSwitch()
        {
            PlayNeogiAsRed();
            _blue.AddToHand(TestData.Cards.CheapCard());
            _green.AddToHand(TestData.Cards.CheapCard());

            var endTurn = new EndTurnCommand();
            endTurn.Execute(_context);

            // Blue is first in seat order after Red.
            Assert.IsTrue(_matchManager.IsResolvingOpponentDiscard);
            Assert.AreEqual(ActionState.TargetingDiscard, _actionSystem.CurrentState);
            Assert.AreEqual(_blue, _context.ActivePlayer, "ActivePlayer should resolve to Blue (ForcedActingPlayer) during Blue's forced discard.");

            var blueCard = _blue.Hand[0];
            new DiscardCardCommand(_blue.Color, blueCard.Id).Execute(_context);

            // Green is next.
            Assert.IsTrue(_matchManager.IsResolvingOpponentDiscard);
            Assert.AreEqual(_green, _context.ActivePlayer, "ActivePlayer should move to Green next.");
            Assert.IsFalse(_blue.Hand.Contains(blueCard));

            var greenCard = _green.Hand[0];
            new DiscardCardCommand(_green.Color, greenCard.Id).Execute(_context);

            // Sequence complete - the real end-of-turn player switch should now have happened.
            Assert.IsFalse(_matchManager.IsResolvingOpponentDiscard);
            Assert.IsFalse(_green.Hand.Contains(greenCard));
            Assert.AreEqual(_blue, _context.ActivePlayer, "Turn rotation should proceed normally to Blue once the discard phase is fully drained.");
        }

        [TestMethod]
        public void EndTurn_TwoNeogisPlayedSameTurn_EachOpponentDiscardsTwice()
        {
            PlayNeogiAsRed();
            PlayNeogiAsRed(); // Second Neogi this turn - stacks
            _blue.AddToHand(TestData.Cards.CheapCard());
            _blue.AddToHand(TestData.Cards.CheapCard());
            _green.AddToHand(TestData.Cards.CheapCard());
            _green.AddToHand(TestData.Cards.CheapCard());

            Assert.HasCount(2, _context.PendingOpponentDiscardTriggers);

            new EndTurnCommand().Execute(_context);

            Assert.AreEqual(_blue, _context.ActivePlayer);
            new DiscardCardCommand(_blue.Color, _blue.Hand[0].Id).Execute(_context);

            // Blue still owes a second discard before moving on to Green.
            Assert.AreEqual(_blue, _context.ActivePlayer, "Blue owes a 2nd discard (stacking) before Green's turn to discard.");
            Assert.HasCount(1, _blue.Hand);

            new DiscardCardCommand(_blue.Color, _blue.Hand[0].Id).Execute(_context);

            Assert.AreEqual(_green, _context.ActivePlayer, "Now Green's turn, owing 2 discards too.");
            Assert.HasCount(2, _green.Hand);
        }

        [TestMethod]
        public void EndTurn_OpponentWithEmptyHand_IsSkipped()
        {
            PlayNeogiAsRed();
            // Blue's hand stays empty - Green has a card.
            _green.AddToHand(TestData.Cards.CheapCard());

            new EndTurnCommand().Execute(_context);

            Assert.AreEqual(_green, _context.ActivePlayer, "Blue should be skipped entirely (empty hand) - Green is next to discard.");
            Assert.IsTrue(_matchManager.IsResolvingOpponentDiscard);

            new DiscardCardCommand(_green.Color, _green.Hand[0].Id).Execute(_context);

            Assert.IsFalse(_matchManager.IsResolvingOpponentDiscard);
            Assert.AreEqual(_blue, _context.ActivePlayer, "Sequence complete - normal turn rotation proceeds to Blue.");
        }
    }
}
