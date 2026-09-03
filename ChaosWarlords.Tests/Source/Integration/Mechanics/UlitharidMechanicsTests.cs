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
    // Ulitharid: "Play a card in the market that costs 4 or less as if it was in your hand,
    // then devour that card."
    [TestClass]
    [TestCategory("Integration")]
    public class UlitharidMechanicsTests
    {
        private MatchContext _context = null!;
        private IGameLogger _logger = null!;
        private MapManager _mapManager = null!;
        private ActionSystem _actionSystem = null!;
        private Player _p1 = null!;
        private ITurnManager _turnManager = null!;
        private IMarketManager _marketManager = null!;
        private List<Card> _marketRow = null!;
        private MatchManager _matchManager = null!;
        private IPlayerStateManager _playerStateManager = null!;
        private MarketStateManager _marketStateManager = null!;

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
            var turnContext = new TurnContext(_p1, _logger);
            _turnManager.CurrentTurnContext.Returns(turnContext);
            // Forward the mock's PlayCard onto the real TurnContext, matching what the real
            // TurnManager.PlayCard would do - needed so aspect-focus assertions below (driven
            // by MatchManager.PlayCardFromMarket's own TurnManager.PlayCard(marketCard) call)
            // actually land somewhere observable.
            _turnManager.When(t => t.PlayCard(Arg.Any<Card>())).Do(call => turnContext.RecordPlayedCard(call.Arg<Card>().Aspect));

            _mapManager = new MapManager(nodes, sites, _turnManager, _logger, _playerStateManager);

            _marketRow = new List<Card>();
            _marketManager = Substitute.For<IMarketManager>();
            _marketManager.MarketRow.Returns(_ => _marketRow);
            _marketManager.When(m => m.RemoveCard(Arg.Any<Card>())).Do(call => _marketRow.Remove(call.Arg<Card>()));

            _actionSystem = new ActionSystem(_turnManager, _mapManager, _logger, _playerStateManager, _marketManager);

            _marketStateManager = new MarketStateManager(_logger);

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
            _actionSystem.SetMarketStateManager(_marketStateManager);

            var victoryManager = Substitute.For<IVictoryManager>();
            _matchManager = new MatchManager(_context, _logger, victoryManager);
            _actionSystem.SetMatchManager(_matchManager);
        }

        private static Card GetUlitharidCard()
        {
            var card = new Card("ulitharid", "Ulitharid", 6, CardAspect.Blasphemy, 3, 6, 0);
            card.AddEffect(new CardEffect(EffectType.PlayFromMarket, 4));
            return card;
        }

        private Card AddMarketCard(string id, int cost, CardAspect aspect, EffectType effectType, ResourceType resource = ResourceType.Power, int amount = 2)
        {
            var card = new Card(id, id, cost, aspect, 1, 1, 0) { Location = CardLocation.Market };
            card.AddEffect(effectType == EffectType.GainResource
                ? new CardEffect(EffectType.GainResource, amount, resource)
                : new CardEffect(effectType, amount));
            _marketRow.Add(card);
            return card;
        }

        [TestMethod]
        public void PlayUlitharid_NoAffordableMarketCard_SkipsWithoutOpeningMarket()
        {
            AddMarketCard("expensive", 5, CardAspect.Warlord, EffectType.GainResource); // Cost > 4

            var ulitharid = GetUlitharidCard();
            _p1.AddToHand(ulitharid);

            new PlayCardCommand(ulitharid).Execute(_context);

            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "No affordable card - the effect should just skip, not wait for a selection.");
            Assert.IsFalse(_marketStateManager.IsOpen);
        }

        [TestMethod]
        public void PlayUlitharid_InstantMarketCardEffect_ResolvesImmediatelyAndDevoursIt()
        {
            var cheapCard = AddMarketCard("cheap_power", 3, CardAspect.Sorcery, EffectType.GainResource, ResourceType.Power, 2);

            var ulitharid = GetUlitharidCard();
            _p1.AddToHand(ulitharid);

            new PlayCardCommand(ulitharid).Execute(_context);

            Assert.AreEqual(ActionState.TargetingPlayFromMarket, _actionSystem.CurrentState);
            Assert.IsTrue(_marketStateManager.IsOpen);
            Assert.IsNotNull(_marketStateManager.DevourCallback);

            var command = _marketStateManager.DevourCallback!(cheapCard) as PlayFromMarketCommand;
            Assert.IsNotNull(command);
            command!.Execute(_context);

            // Instant effect (GainResource) - resolves synchronously, one-shot subscription
            // fires within the same call.
            Assert.AreEqual(2, _p1.Power, "The market card's own effect should have applied to the player.");
            Assert.AreEqual(CardLocation.Void, cheapCard.Location, "Market card should end up devoured (Void), not left in the market or moved to Hand/PlayedCards.");
            Assert.DoesNotContain(cheapCard, _marketRow);
            Assert.Contains(cheapCard, _context.VoidPile);
            Assert.IsFalse(_p1.Hand.Contains(cheapCard));
            Assert.IsFalse(_p1.PlayedCards.Contains(cheapCard));
            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.GetAspectCount(CardAspect.Sorcery), "Aspect-focus tracking should credit the MARKET CARD's own aspect (Sorcery), not Ulitharid's (Blasphemy).");
            Assert.AreEqual(1, _context.TurnManager.CurrentTurnContext.GetAspectCount(CardAspect.Blasphemy), "Ulitharid's own aspect should be credited exactly once (by its own PlayCardCommand), not again by playing the market card.");
        }

        [TestMethod]
        public void PlayUlitharid_MarketCardNeedsItsOwnTargeting_DevoursOnlyAfterThatResolves()
        {
            // PlaceSpy requires its own follow-up click (a site) before the chain completes -
            // proves the one-shot OnActionCompleted subscription survives a multi-frame
            // nested resolution, not just an instant effect.
            var spyCard = AddMarketCard("needs_targeting", 2, CardAspect.Shadow, EffectType.PlaceSpy, amount: 1);
            _p1.SpiesInBarracks = 1;

            var site = TestData.Sites.NeutralSite();
            site.Id = 1;
            var node = TestData.MapNodes.Node1();
            site.AddNode(node);
            _mapManager.NodesInternal.Add(node);
            _mapManager.SitesInternal.Add(site);

            var ulitharid = GetUlitharidCard();
            _p1.AddToHand(ulitharid);

            new PlayCardCommand(ulitharid).Execute(_context);

            var command = _marketStateManager.DevourCallback!(spyCard) as PlayFromMarketCommand;
            Assert.IsNotNull(command);
            command!.Execute(_context);

            // Still mid-flight: PlaceSpy itself needs a site click - the market card must NOT
            // be devoured yet.
            Assert.AreEqual(ActionState.TargetingPlaceSpy, _actionSystem.CurrentState);
            Assert.AreEqual(CardLocation.Market, spyCard.Location, "Should not be devoured until its own PlaceSpy targeting actually resolves.");
            Assert.Contains(spyCard, _marketRow);

            // Player clicks the site.
            var placeCmd = _actionSystem.HandleTargetClick(null, site) as PlaceSpyCommand;
            Assert.IsNotNull(placeCmd);
            placeCmd!.Execute(_context);

            Assert.Contains(_p1.Color, site.Spies, "The spy should have been placed.");
            Assert.AreEqual(CardLocation.Void, spyCard.Location, "Now that its own effect fully resolved, the market card should be devoured.");
            Assert.DoesNotContain(spyCard, _marketRow);
        }

        [TestMethod]
        public void PlayFromMarketCommand_ServerSideCostRecheck_RejectsOverBudgetCard()
        {
            var expensiveCard = AddMarketCard("sneaky_expensive", 5, CardAspect.Warlord, EffectType.GainResource);
            var ulitharid = GetUlitharidCard();
            _p1.AddToHand(ulitharid);

            // Bypass the client-side filter entirely - directly build a command as if a
            // compromised/buggy client sent it.
            new PlayCardCommand(ulitharid).Execute(_context); // Enters TargetingPlayFromMarket with no valid targets? No affordable card was added below cost 4 here on purpose.
            var command = new PlayFromMarketCommand(expensiveCard.RuntimeId, expensiveCard.Id);

            Assert.IsFalse(command.Validate(_context), "A market card over the cost cap must be rejected server-side, regardless of what the client sent.");
        }
    }
}
