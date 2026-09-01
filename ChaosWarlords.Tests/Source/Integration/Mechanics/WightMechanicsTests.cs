using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ChaosWarlords.Tests.Source.Integration.Mechanics
{
    [TestClass]
    [TestCategory("Integration")]
    public class WightMechanicsTests
    {
        private MatchContext _context = null!;
        private IGameLogger _logger = null!;
        private MapManager _mapManager = null!; // Real
        private ActionSystem _actionSystem = null!;
        private Player _p1 = null!;
        private Player _p2 = null!;
        private ITurnManager _turnManager = null!;
        private IMarketManager _marketManager = null!;
        private MatchManager _matchManager = null!; 
        private IPlayerStateManager _playerStateManager = null!;
        
        private MapNode _nodeA = null!;
        private MapNode _nodeB = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();
            
            _nodeA = new MapNode(1, ChaosWarlords.Source.Core.Data.LogicVector2.Zero);
            _nodeB = new MapNode(2, new ChaosWarlords.Source.Core.Data.LogicVector2(100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 0));
            _nodeA.AddNeighbor(_nodeB);
            
            var nodes = new List<MapNode> { _nodeA, _nodeB };
            var sites = new List<Site>();
            // Lookup not strictly needed for basic map manager constructor if sites are empty/managed internally, 
            // but MapManager builds its own lookup.
            // MapManager constructor does NOT take lookup. It takes TurnManager.

            // Fix Players (Error 2/3)
            _p1 = new Player(PlayerColor.Red);
            _p2 = new Player(PlayerColor.Blue);
            
            _p1.TroopsInBarracks = 10;
            _p2.TroopsInBarracks = 10;

            _turnManager = Substitute.For<ITurnManager>();
            _playerStateManager = new PlayerStateManager(_logger); // Moved up
            _turnManager.ActivePlayer.Returns(_p1);
            _turnManager.Players.Returns(new List<Player> { _p1, _p2 });
            _turnManager.CurrentTurnContext.Returns(new TurnContext(_p1, _logger)); 

            // Fix MapManager (Error 1)
            // MapManager(nodes, sites, turnManager, logger, playerState)
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

        [TestMethod]
        public void PlayWight_WithValidTargets_ShouldRequestPopup()
        {
            _nodeA.Occupant = PlayerColor.Red;
            _nodeB.Occupant = PlayerColor.Blue;

            var wight = GetWightCard();
            var noble = new Card("noble", "Noble", 3, CardAspect.Blasphemy, 1, 1, 0);
            
            _p1.AddToHand(wight);
            _p1.AddToHand(noble);
            
            bool popupRequested = false;
            _actionSystem.OnInteractionRequested += req =>
            {
                popupRequested = true;
                req.OnResponse(false); // Decline
            };

            var command = new PlayCardCommand(wight);
            command.Execute(_context);

            Assert.IsTrue(popupRequested, "Optional Effect Popup should be requested when valid targets exist.");
        }

        [TestMethod]
        public void PlayWight_WithNoAssassinationTarget_ShouldSkipPopup()
        {
            _nodeA.Occupant = PlayerColor.Red;
            _nodeB.Occupant = PlayerColor.None; 

            var wight = GetWightCard();
            var noble = new Card("noble", "Noble", 3, CardAspect.Blasphemy, 1, 1, 0);
            
            _p1.AddToHand(wight);
            _p1.AddToHand(noble);

            bool popupRequested = false;
            _actionSystem.OnInteractionRequested += req => popupRequested = true;

            var command = new PlayCardCommand(wight);
            command.Execute(_context);

            Assert.IsFalse(popupRequested, "Optional Effect Popup should be SKIPPED when NO valid supplant targets exist.");
            Assert.AreEqual(2, _p1.Power, "Choose-one: with the Devour->Supplant half impossible, the +2 Power Alternative should fire instead - not silently nothing.");
        }


        [TestMethod]
        public void PlayWight_WithNoTroopsInBarracks_StillRequestsPopup()
        {
            // Rulebook p.12/22 ("Deploy a Troop," cross-referenced by "Supplant a Troop,"
            // p.14): an empty barracks doesn't make Supplant impossible - the assassinate
            // half is still legal and the deploy half grants 1 VP instead of placing a
            // troop, so the Devour->Supplant alternative is still available; it should NOT
            // fall back to the +2 Power alternative.
            // Valid Target situation
            _nodeA.Occupant = PlayerColor.Red;
            _nodeB.Occupant = PlayerColor.Blue;

            // No troops in barracks - Supplant is still legal (grants 1 VP instead of deploying)
            _p1.TroopsInBarracks = 0;

            var wight = GetWightCard();
            var noble = new Card("noble", "Noble", 3, CardAspect.Blasphemy, 1, 1, 0);

            _p1.AddToHand(wight);
            _p1.AddToHand(noble);

            bool popupRequested = false;
            _actionSystem.OnInteractionRequested += req =>
            {
                popupRequested = true;
                req.OnResponse(false); // Decline
            };

            var command = new PlayCardCommand(wight);
            command.Execute(_context);

            Assert.IsTrue(popupRequested, "Optional Effect Popup should still be requested - Supplant remains legal with an empty barracks.");
        }

        [TestMethod]
        public void PlayWight_WithEmptyHand_ShouldGrantPowerFallback()
        {
            // Regression test for the top-level HasValidTargets pre-check fix (see planning.txt
            // RESOLVED): before, playing Wight with nothing else in hand skipped the Devour
            // effect (no valid Hand target) and granted NOTHING - not even the +2 Power
            // fallback, since the old JSON had no Alternative wiring at all.
            _nodeA.Occupant = PlayerColor.Red;
            _nodeB.Occupant = PlayerColor.Blue; // Valid Supplant target - irrelevant here, the hand itself is empty

            var wight = GetWightCard();
            _p1.AddToHand(wight); // Only card in hand

            bool popupRequested = false;
            _actionSystem.OnInteractionRequested += req => popupRequested = true;

            var command = new PlayCardCommand(wight);
            command.Execute(_context);

            Assert.IsFalse(popupRequested, "No valid Devour target (empty hand) means no popup - the Alternative fires directly.");
            Assert.AreEqual(2, _p1.Power, "Empty-hand fallback should still grant the +2 Power Alternative.");
        }

        [TestMethod]
        public void PlayWight_AcceptThenDevourThenSupplant_ActuallyDevoursTheSelectedCard()
        {
            // Regression test for a full logic review (2026-08-30, see planning.txt
            // RESOLVED): the popup-only tests above never exercised past accepting the
            // popup, so nothing pinned down that the card picked as Wight's "cost" is
            // actually removed from the player's hand once the whole chain completes.
            // This plays the full sequence a real click-through would: accept the
            // optional Devour -> pick a hand card to devour -> pick a Supplant target -
            // through the real MatchManager/MapManager (not mocks), the same way
            // CommandDispatcher would dispatch each command in live play.
            _nodeA.Occupant = PlayerColor.Red;
            _nodeB.Occupant = PlayerColor.Blue;

            var wight = GetWightCard();
            var noble = new Card("noble", "Noble", 3, CardAspect.Blasphemy, 1, 1, 0);

            _p1.AddToHand(wight);
            _p1.AddToHand(noble);

            InteractionRequest? capturedRequest = null;
            int requestCount = 0;
            _actionSystem.OnInteractionRequested += req => { capturedRequest = req; requestCount++; };

            var playCommand = new PlayCardCommand(wight);
            playCommand.Execute(_context);

            // Regression test for a real bug found via manual playtesting (2026-08-31, see
            // planning.txt RESOLVED): MatchManager.PlayCard called ActionSystem.ProcessStack()
            // a second, redundant time right after CardEffectProcessor.ResolveEffects already
            // did so internally. For a blocking/optional effect (like Wight's Devour), that
            // meant the still-pending top-of-stack effect got processed twice per single card
            // play, firing the interaction request twice for one player action.
            Assert.AreEqual(1, requestCount, "Playing Wight once should raise OnInteractionRequested exactly once, not twice.");
            Assert.IsNotNull(capturedRequest, "Popup should have been requested via OnInteractionRequested.");
            capturedRequest!.OnResponse(true); // Accept

            Assert.AreEqual(ActionState.TargetingDevourHand, _actionSystem.CurrentState, "Accepting should enter Devour-Hand targeting.");

            // Player clicks "noble" in hand to pay Wight's devour cost.
            var devourCommand = _actionSystem.HandleDevourSelection(noble);
            Assert.IsNotNull(devourCommand);
            Assert.IsFalse(devourCommand!.IsDeferred, "Today's live flow devours immediately on selection rather than deferring - see planning.txt RESOLVED.");
            devourCommand.Execute(_context);

            // The devour should have already happened - BEFORE the Supplant target is even picked.
            Assert.IsFalse(_p1.Hand.Contains(noble), "Noble should have left the hand once devoured.");
            Assert.AreEqual(CardLocation.Void, noble.Location);
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState, "Devour resolving should chain into Supplant targeting.");

            // Player clicks nodeB (the enemy troop) as the Supplant target.
            var supplantCommand = _actionSystem.HandleTargetClick(_nodeB, null);
            Assert.IsNotNull(supplantCommand);
            supplantCommand!.Execute(_context);

            // Assert: Supplant succeeded, and the devoured card is still gone (not double-devoured,
            // not resurrected, not left dangling as a stale PendingDevourCard).
            Assert.AreEqual(PlayerColor.Red, _nodeB.Occupant, "Supplant should have placed Red's troop at nodeB.");
            Assert.AreEqual(1, _p1.TrophyHall, "Supplant's assassinate half should award a trophy.");
            Assert.IsFalse(_p1.Hand.Contains(noble));
            Assert.AreEqual(CardLocation.Void, noble.Location);
            Assert.IsNull(_actionSystem.PendingDevourCard, "No pending devour should be left dangling after the chain completes.");
            Assert.AreEqual(0, _p1.Power, "Choose-one mutual exclusivity: accepting the Devour->Supplant half must NOT also grant the +2 Power Alternative.");

            // Regression assertion for the same bug: the double-processed stack left a second,
            // un-consumed TargetingSupplant effect buried underneath, which would resurface
            // and force Supplant targeting on the next unrelated card played this turn.
            Assert.IsEmpty(_actionSystem.ExecutionStack, "Execution stack should be fully empty after the chain completes - no leftover effects to ambush the next card played.");
        }

        private Card GetWightCard()
        {
            // Matches the real card's "Choose one: +2 Power. Or, devour a card in your hand
            // to Supplant a troop." - a single mutually-exclusive effect via Alternative, not
            // an unconditional Power gain plus a separate optional add-on (see planning.txt
            // RESOLVED for the bug this replaced).
            var card = new Card("wight", "Wight", 3, CardAspect.Oblivion, 1, 3, 0);

            var devour = new CardEffect(EffectType.Devour, 1)
            {
                TargetLocation = CardLocation.Hand,
                IsOptional = true,
                OnSuccess = new CardEffect(EffectType.Supplant, 1),
                Alternative = new CardEffect(EffectType.GainResource, 2, ResourceType.Power)
            };
            card.AddEffect(devour);

            return card;
        }
    }
}
