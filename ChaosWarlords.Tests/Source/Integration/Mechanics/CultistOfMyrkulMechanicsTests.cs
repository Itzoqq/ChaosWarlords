using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts;
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
    // Regression coverage for a real, already-shipped bug found while cross-checking
    // cards.json against the real card image (2026-09-01, see planning.txt RESOLVED): the
    // real card is "Choose one: Gain 2 Influence. Or, devour this card; at end of turn,
    // promote up to 2 other cards played this turn." - the old implementation devoured from
    // InnerCircle (wrong location - should be Self), fabricated an extra "+3 Influence" step
    // not on the real card, and wasn't mutually exclusive (a player accepting the devour kept
    // the Influence gain too).
    [TestClass]
    [TestCategory("Integration")]
    public class CultistOfMyrkulMechanicsTests
    {
        private MatchContext _context = null!;
        private IGameLogger _logger = null!;
        private MapManager _mapManager = null!; // Real
        private ActionSystem _actionSystem = null!;
        private Player _p1 = null!;
        private ITurnManager _turnManager = null!;
        private IMarketManager _marketManager = null!;
        private MatchManager _matchManager = null!;
        private IPlayerStateManager _playerStateManager = null!;
        private TurnContext _turnContext = null!;

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
            _turnContext = new TurnContext(_p1, _logger);
            _turnManager.CurrentTurnContext.Returns(_turnContext);

            _mapManager = new MapManager(nodes, sites, _turnManager, _logger, _playerStateManager);
            _actionSystem = new ActionSystem(_turnManager, _mapManager, _logger);

            _marketManager = Substitute.For<IMarketManager>();
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

        [TestMethod]
        public void PlayCultist_DeclineDevour_GrantsInfluenceAlternative()
        {
            var cultist = GetCultistCard();
            _p1.AddToHand(cultist);

            bool popupRequested = false;
            _actionSystem.OnInteractionRequested += req =>
            {
                popupRequested = true;
                req.OnResponse(false); // Decline
            };

            var command = new PlayCardCommand(cultist);
            command.Execute(_context);

            Assert.IsTrue(popupRequested, "Self-devour is always a valid target (the card itself), so the popup should be requested.");
            Assert.AreEqual(2, _p1.Influence, "Declining should grant the +2 Influence Alternative.");
            Assert.AreEqual(0, _turnContext.PendingPromotionsCount, "Declining must not also bank a promotion credit.");
        }

        [TestMethod]
        public void PlayCultist_AcceptDevour_DevoursSelfAndBanksPromotionCredit_NotInfluence()
        {
            var cultist = GetCultistCard();
            _p1.AddToHand(cultist);

            InteractionRequest? capturedRequest = null;
            _actionSystem.OnInteractionRequested += req => capturedRequest = req;

            var command = new PlayCardCommand(cultist);
            command.Execute(_context);

            Assert.IsNotNull(capturedRequest);
            capturedRequest!.OnResponse(true); // Accept

            // Devour(Self) applies its OnSuccess immediately but - matching the established
            // Skeletal Horde pattern (see SelfDevourIntegrationTests) - defers the actual
            // move-to-Void until end of turn via CardsMarkedForTurnEndDevour, so the card
            // stays visibly "in play" for the rest of the turn.
            Assert.AreEqual(CardLocation.Played, cultist.Location, "Self-devour stays 'Played' until end of turn.");
            CollectionAssert.Contains(_context.CardsMarkedForTurnEndDevour, cultist, "Card should be marked for end-of-turn devour.");
            Assert.AreEqual(2, _turnContext.PendingPromotionsCount, "Accepting should bank 2 Promote credits (\"promote up to 2\") for later, not resolve them immediately.");
            Assert.AreEqual(0, _p1.Influence, "Choose-one mutual exclusivity: accepting the devour must NOT also grant the +2 Influence Alternative.");
        }

        private Card GetCultistCard()
        {
            var card = new Card("cultist_of_myrkul", "Cultist of Myrkul", 2, CardAspect.Oblivion, 1, 2, 0);

            var devour = new CardEffect(EffectType.Devour, 1)
            {
                TargetLocation = CardLocation.Self,
                IsOptional = true,
                OnSuccess = new CardEffect(EffectType.Promote, 2),
                Alternative = new CardEffect(EffectType.GainResource, 2, ResourceType.Influence)
            };
            card.AddEffect(devour);

            return card;
        }
    }
}
