using ChaosWarlords.Source.Contexts;
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

            var actionSystem = new ActionSystem(turnManager, _mapManager, logger);
            var playerState = new PlayerStateManager(logger);
            actionSystem.SetPlayerStateManager(playerState);
            actionSystem.SetMarketManager(_marketManager);

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
    }
}
