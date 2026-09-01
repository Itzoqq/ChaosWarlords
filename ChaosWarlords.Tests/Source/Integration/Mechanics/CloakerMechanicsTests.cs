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
    // Cloaker: "Choose one: Place a spy. Or, return one of your spies to assassinate a
    // troop at that spy's site." Simpler than planning.txt originally assumed - no
    // origin-site *history* tracking, just "the site you returned the spy from just now"
    // (ActionSystem.PendingSite).
    [TestClass]
    [TestCategory("Integration")]
    public class CloakerMechanicsTests
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

        private Site _siteA = null!; // Where Red's own spy starts
        private Site _siteB = null!; // No Red spy here
        private MapNode _nodeA = null!; // Enemy troop, inside Site A
        private MapNode _nodeB = null!; // Enemy troop, inside Site B
        private MapNode _redTroopNode = null!; // Red's own troop, neighboring Site A - gives
                                                // Red Presence at Site A via adjacency even
                                                // after the spy that used to grant it there
                                                // is returned (Assassinate requires Presence
                                                // same as every other assassinate-granting
                                                // card - this isn't special-cased for Cloaker).

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();

            _nodeA = TestData.MapNodes.Node1();
            _nodeB = TestData.MapNodes.Node2();
            _nodeA.Occupant = PlayerColor.Blue;
            _nodeB.Occupant = PlayerColor.Blue;

            _siteA = TestData.Sites.NeutralSite();
            _siteA.Id = 1;
            _siteA.AddNode(_nodeA);
            _siteB = TestData.Sites.NeutralSite();
            _siteB.Id = 2;
            _siteB.AddNode(_nodeB);

            _redTroopNode = TestData.MapNodes.Node3();
            _redTroopNode.Occupant = PlayerColor.Red;
            _nodeA.AddNeighbor(_redTroopNode);
            // Also adjacent to Site B's node, so Presence isn't what distinguishes the two
            // sites in the "wrong site" test below - only the PendingSite scoping guard is.
            _nodeB.AddNeighbor(_redTroopNode);

            var nodes = new List<MapNode> { _nodeA, _nodeB, _redTroopNode };
            var sites = new List<Site> { _siteA, _siteB };

            _p1 = new Player(PlayerColor.Red);
            var p2 = new Player(PlayerColor.Blue);

            _turnManager = Substitute.For<ITurnManager>();
            _playerStateManager = new PlayerStateManager(_logger);
            _turnManager.ActivePlayer.Returns(_p1);
            _turnManager.Players.Returns(new List<Player> { _p1, p2 });
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

        private static Card GetCloakerCard()
        {
            var card = new Card("cloaker", "Cloaker", 2, CardAspect.Shadow, 1, 3, 0);
            var placeSpy = new CardEffect(EffectType.PlaceSpy, 1)
            {
                IsOptional = true,
                Alternative = new CardEffect(EffectType.ReturnOwnSpy, 1)
                {
                    OnSuccess = new CardEffect(EffectType.Assassinate, 1)
                }
            };
            card.AddEffect(placeSpy);
            return card;
        }

        [TestMethod]
        public void PlayCloaker_AcceptPlaceSpy_DoesNotReturnOrAssassinate()
        {
            _siteA.Spies.Add(_p1.Color); // Red already has a spy at Site A

            var cloaker = GetCloakerCard();
            _p1.AddToHand(cloaker);

            InteractionRequest? captured = null;
            _actionSystem.OnInteractionRequested += req => captured = req;

            new PlayCardCommand(cloaker).Execute(_context);
            Assert.IsNotNull(captured);
            captured!.OnResponse(true); // Accept: Place a spy

            Assert.AreEqual(ActionState.TargetingPlaceSpy, _actionSystem.CurrentState);

            var placeCmd = _actionSystem.HandleTargetClick(null, _siteB) as PlaceSpyCommand;
            Assert.IsNotNull(placeCmd);
            placeCmd!.Execute(_context);

            Assert.Contains(_p1.Color, _siteB.Spies, "New spy should be placed at Site B.");
            Assert.Contains(_p1.Color, _siteA.Spies, "Original spy at Site A should be untouched - the Alternative never fired.");
            Assert.AreEqual(PlayerColor.Blue, _nodeA.Occupant, "No assassination should have happened.");
            Assert.AreEqual(PlayerColor.Blue, _nodeB.Occupant);
        }

        [TestMethod]
        public void PlayCloaker_DeclinePlaceSpy_ReturnsOwnSpyThenAssassinatesOnlyAtThatSite()
        {
            _siteA.Spies.Add(_p1.Color);

            var cloaker = GetCloakerCard();
            _p1.AddToHand(cloaker);

            InteractionRequest? captured = null;
            _actionSystem.OnInteractionRequested += req => captured = req;

            new PlayCardCommand(cloaker).Execute(_context);
            Assert.IsNotNull(captured);
            captured!.OnResponse(false); // Decline: use the Alternative instead

            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, _actionSystem.CurrentState);

            var returnCmd = _actionSystem.HandleTargetClick(null, _siteA) as ReturnOwnSpyCommand;
            Assert.IsNotNull(returnCmd);
            returnCmd!.Execute(_context);

            Assert.DoesNotContain(_p1.Color, _siteA.Spies, "Spy should have left Site A.");
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState, "Should chain straight into Assassinate.");

            // Wrong site: Site B's troop is NOT where the spy was returned from.
            var rejectedCmd = _actionSystem.HandleTargetClick(_nodeB, null);
            Assert.IsNull(rejectedCmd, "Assassinating at the wrong site should be rejected.");
            Assert.AreEqual(PlayerColor.Blue, _nodeB.Occupant, "Wrong-site troop must survive.");

            // Correct site: Site A's troop.
            var assassinateCmd = _actionSystem.HandleTargetClick(_nodeA, null);
            Assert.IsNotNull(assassinateCmd);
            assassinateCmd!.Execute(_context);

            Assert.AreEqual(PlayerColor.None, _nodeA.Occupant, "Correct-site troop should be assassinated.");
            Assert.AreEqual(1, _p1.TrophyHall);
        }

        [TestMethod]
        public void PlayCloaker_NoOwnSpiesAnywhere_DecliningFizzlesInsteadOfStalling()
        {
            // No spy placed anywhere for Red - the Alternative (ReturnOwnSpy) has no valid
            // target. Regression test for the PushEffectNode dead-branch guard: without it,
            // declining here would leave CurrentState stuck in TargetingReturnOwnSpy forever,
            // waiting for a click that can never resolve to a real spy.
            var cloaker = GetCloakerCard();
            _p1.AddToHand(cloaker);

            InteractionRequest? captured = null;
            _actionSystem.OnInteractionRequested += req => captured = req;

            new PlayCardCommand(cloaker).Execute(_context);
            Assert.IsNotNull(captured);
            captured!.OnResponse(false); // Decline

            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "With no spy to return and no further Alternative, the chain should cleanly end at Normal, not stall.");
            Assert.AreEqual(PlayerColor.Blue, _nodeA.Occupant);
            Assert.AreEqual(PlayerColor.Blue, _nodeB.Occupant);
        }
    }
}
