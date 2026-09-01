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
using System.Linq;

namespace ChaosWarlords.Tests.Source.Integration.Mechanics
{
    [TestClass]
    [TestCategory("Integration")]
    public class InsaneOutcastMechanicsTests
    {
        private MatchContext _context = null!;
        private IGameLogger _logger = null!;
        private MapManager _mapManager = null!;
        private ActionSystem _actionSystem = null!;
        private Player _p1 = null!;
        private ITurnManager _turnManager = null!;
        private IMarketManager _marketManager = null!;
        private MatchManager _matchManager = null!;
        private IPlayerStateManager _playerStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();

            var nodes = new List<MapNode>();
            var sites = new List<Site>();

            _p1 = new Player(PlayerColor.Red);
            var p2 = new Player(PlayerColor.Blue);

            _turnManager = Substitute.For<ITurnManager>();
            _playerStateManager = new PlayerStateManager(_logger);
            _turnManager.ActivePlayer.Returns(_p1);
            _turnManager.Players.Returns(new List<Player> { _p1, p2 });
            _turnManager.GetPlayerByColor(_p1.Color).Returns(_p1);
            _turnManager.GetPlayerByColor(p2.Color).Returns(p2);
            _turnManager.CurrentTurnContext.Returns(new TurnContext(_p1, _logger));

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

        private Card GetInsaneOutcastCard()
        {
            var card = new Card("insane_outcast", "Insane Outcast", 0, CardAspect.Neutral, -1, 0, 0)
            {
                RedirectsToSupplyOnDevourOrPromote = true
            };

            var discard = new CardEffect(EffectType.DiscardCard, 1)
            {
                TargetLocation = CardLocation.Hand,
                OnSuccess = new CardEffect(EffectType.Devour, 0) { TargetLocation = CardLocation.Self }
            };
            card.AddEffect(discard);

            return card;
        }

        [TestMethod]
        public void PlayInsaneOutcast_DiscardingACard_ReturnsItselfToSupply_NotVoid()
        {
            var outcast = GetInsaneOutcastCard();
            var filler = TestData.Cards.CheapCard();
            _p1.AddToHand(outcast);
            _p1.AddToHand(filler);

            var playCommand = new PlayCardCommand(outcast);
            playCommand.Execute(_context);

            Assert.AreEqual(ActionState.TargetingDiscard, _actionSystem.CurrentState, "Playing Insane Outcast should immediately demand a discard.");

            // Player clicks "filler" in hand to pay the discard cost.
            var discardCommand = new DiscardCardCommand(_p1.Color, filler.Id);
            discardCommand.Execute(_context);

            Assert.IsFalse(_p1.Hand.Contains(filler), "Filler card should have left the hand.");
            Assert.AreEqual(CardLocation.DiscardPile, filler.Location);

            Assert.AreEqual(CardLocation.Supply, outcast.Location, "Insane Outcast should redirect to Supply, not Void, when its own chain devours it.");
            CollectionAssert.DoesNotContain(_context.VoidPile, outcast, "Should never enter the void pile.");
            CollectionAssert.DoesNotContain(_context.CardsMarkedForTurnEndDevour, outcast, "Unlike a normal self-devour (Skeletal Horde), this resolves immediately, not deferred to end of turn.");
        }

        [TestMethod]
        public void DevourCard_DirectlyTargetingInsaneOutcast_RedirectsToSupply()
        {
            // Simulates a hypothetical OTHER card's "devour a card from hand" effect
            // targeting an Insane Outcast sitting in a player's hand - the redirect rule
            // ("if Insane Outcast would be devoured... return it to the supply instead")
            // must apply regardless of which devour path triggers it, not just its own.
            var outcast = GetInsaneOutcastCard();
            _p1.AddToHand(outcast);

            _playerStateManager.DevourCard(_p1, outcast);

            Assert.AreEqual(CardLocation.Supply, outcast.Location);
            CollectionAssert.DoesNotContain(_context.VoidPile, outcast);
        }

        [TestMethod]
        public void TryPromoteCard_DirectlyTargetingInsaneOutcast_RedirectsToSupply()
        {
            var outcast = GetInsaneOutcastCard();
            _p1.AddToHand(outcast);

            bool success = _playerStateManager.TryPromoteCard(_p1, outcast, out var error);

            Assert.IsTrue(success, "Redirecting counts as success, not a promotion failure.");
            Assert.AreEqual(CardLocation.Supply, outcast.Location);
            CollectionAssert.DoesNotContain(_p1.InnerCircle.ToList(), outcast, "Should never actually enter the Inner Circle.");
        }

        [TestMethod]
        public void NegativeDeckVP_FlowsIntoVictoryManagerCalculation()
        {
            var outcast = GetInsaneOutcastCard();
            _p1.AddToHand(outcast); // Still "in the deck" for scoring purposes while in Hand

            var victoryManager = new VictoryManager(_logger);
            int deckVpTotal = victoryManager.GetScoreBreakdown(_p1, _context).DeckVP;

            Assert.AreEqual(-1, deckVpTotal, "Insane Outcast's -1 DeckVP should subtract from the total, not be clamped at 0.");
        }

        [TestMethod]
        public void GetAllMarketCards_ExcludesInsaneOutcast()
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!System.IO.File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using (var stream = System.IO.File.OpenRead(path))
            {
                database.Load(stream);
            }

            var marketCards = database.GetAllMarketCards();

            Assert.IsFalse(marketCards.Any(c => c.Id.StartsWith("insane_outcast")), "Insane Outcast must never be purchasable from the market.");
        }
    }
}
