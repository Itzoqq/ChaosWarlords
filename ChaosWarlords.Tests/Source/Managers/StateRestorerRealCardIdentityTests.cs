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
    /// StateRestorerTests.cs (and every other pre-existing test exercising StateRestorer)
    /// stubs ICardDatabase.GetCardById against a dictionary keyed by the same PLAIN id used to
    /// hand-construct each test Card - never through the real CardFactory/CardDatabase
    /// pipeline. That masked a real bug: CardFactory.GenerateUniqueId always suffixes
    /// Card.Id (e.g. "wight" -> "wight_a3f2c1"), but CardDto.DefinitionId used to be sourced
    /// from Card.Id - so ICardDatabase.GetCardById(DefinitionId) could NEVER match a real
    /// catalog entry, and every restore (StateRestorer.RestoreState, reached by
    /// ActionSystem.CancelTargeting on any ordinary targeting cancel, not just
    /// CommandDispatcher's exception rollback) silently emptied every card collection instead
    /// of round-tripping it. This file uses the REAL CardFactory/CardDatabase (a real,
    /// suffixed Card.Id) end to end through StateRestorer specifically to catch that class of
    /// regression - see planning.txt.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class StateRestorerRealCardIdentityTests
    {
        private const string CatalogJson = @"
            [
              { ""id"": ""wight"", ""cost"": 3, ""aspect"": ""Neutral"", ""deckVP"": 1, ""innerCircleVP"": 3, ""effects"": [] }
            ]";

        private MatchContext _context = null!;
        private Player _player = null!;
        private ICardDatabase _cardDb = null!;

        [TestInitialize]
        public void Setup()
        {
            TestLogger.Initialize();
            var logger = TestLogger.Instance;

            var localization = new TestLocalizationService(new()
            {
                ["wight_name"] = "Wight",
                ["wight_description"] = "Test card",
            });
            var db = new CardDatabase(localization);
            db.LoadFromJson(CatalogJson);
            _cardDb = db;

            _player = new Player(PlayerColor.Red, displayName: "Player 1");
            _player.SeatIndex = 0;

            var turnManager = new TurnManager(
                new List<Player> { _player },
                new SeededGameRandom(12345, logger),
                logger);

            var mapManager = Substitute.For<IMapManager>();
            mapManager.Nodes.Returns(new List<MapNode>());
            mapManager.Sites.Returns(new List<Site>());

            var marketManager = Substitute.For<IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card>());

            var actionSystem = new ActionSystem(turnManager, mapManager, logger);
            var playerState = new PlayerStateManager(logger);
            actionSystem.SetPlayerStateManager(playerState);
            actionSystem.SetMarketManager(marketManager);

            _context = new MatchContext(turnManager, mapManager, marketManager, actionSystem, _cardDb, playerState, logger, seed: 999);
            actionSystem.SetMatchContext(_context);

            var matchManager = new MatchManager(_context, logger, Substitute.For<IVictoryManager>());
            actionSystem.SetMatchManager(matchManager);
        }

        [TestMethod]
        public void RestoreState_WithRealCardFactoryIdentity_RestoresTheCardInsteadOfEmptyingTheHand()
        {
            // Arrange: a REAL card, so Id carries CardFactory.GenerateUniqueId's suffix.
            var wight = _cardDb.GetCardById("wight", _context.Random)!;
            Assert.AreNotEqual("wight", wight.Id, "Sanity check: CardFactory should have suffixed Id.");
            Assert.AreEqual("wight", wight.DefinitionId);

            _player.AddToHand(wight);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            // Act: clear the hand (simulating whatever a command/targeting sequence did before
            // failing/cancelling), then restore.
            _player.ClearHand();
            Assert.IsEmpty(_player.Hand);

            StateRestorer.RestoreState(_context, snapshot);

            // Assert: the card actually came back - not an empty hand.
            Assert.HasCount(1, _player.Hand, "The real card should round-trip through a restore, not vanish.");
            Assert.AreEqual("wight", _player.Hand[0].DefinitionId);
            Assert.AreEqual("Wight", _player.Hand[0].Name);
        }

        [TestMethod]
        public void RestoreState_RestoresLocationOnRealCards()
        {
            var wight = _cardDb.GetCardById("wight", _context.Random)!;
            wight.Location = CardLocation.Played;
            _player.AddToPlayed(wight);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _player.ClearPlayed();
            StateRestorer.RestoreState(_context, snapshot);

            Assert.HasCount(1, _player.PlayedCards);
            Assert.AreEqual(CardLocation.Played, _player.PlayedCards[0].Location);
        }

        [TestMethod]
        public void RestoreState_RestoresRuntimeIdOnRealCards()
        {
            var wight = _cardDb.GetCardById("wight", _context.Random)!;
            var originalRuntimeId = wight.RuntimeId;
            _player.AddToHand(wight);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _player.ClearHand();
            StateRestorer.RestoreState(_context, snapshot);

            Assert.HasCount(1, _player.Hand);
            Assert.AreEqual(originalRuntimeId, _player.Hand[0].RuntimeId,
                "A pending command/UI selection holding the pre-restore RuntimeId should still find this card.");
        }

        [TestMethod]
        public void RestoreState_RestoresHardcodedStartingDeckCards_SoldierAndNoble()
        {
            // Soldier/Noble aren't in cards.json at all (CardFactory.CreateSoldier/CreateNoble
            // hardcodes them) - GetCardById needs its own synthetic-definition fallback for
            // these, independent of the suffix fix, or every starting deck would fail to
            // restore.
            var soldier = _cardDb.GetCardById("soldier", _context.Random)!;
            var noble = _cardDb.GetCardById("noble", _context.Random)!;
            Assert.IsNotNull(soldier, "CardDatabase should resolve the hardcoded 'soldier' definition.");
            Assert.IsNotNull(noble, "CardDatabase should resolve the hardcoded 'noble' definition.");

            _player.DeckManager.ForceAdd(soldier);
            _player.DeckManager.ForceAdd(noble);
            var snapshot = DtoMapper.ToGameStateDto(_context);

            _player.DeckManager.Clear();
            StateRestorer.RestoreState(_context, snapshot);

            Assert.HasCount(2, _player.Deck);
        }
    }
}
