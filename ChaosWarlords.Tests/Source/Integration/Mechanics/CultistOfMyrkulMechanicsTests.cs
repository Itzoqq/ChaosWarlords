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
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Tests.Source.Doubles.State;
using Microsoft.Xna.Framework;
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
            _marketManager = Substitute.For<IMarketManager>();
            // A real (even if empty) MarketRow, not an unconfigured null - MatchContext.
            // GetStateHash/DtoMapper.ToGameStateDto both dereference it, and ActionSystem's
            // targeting-sequence snapshot (CancelTargeting's real revert mechanism) silently
            // no-ops if snapshotting throws (see ActionSystem.TryCreateTargetingSnapshot) -
            // without this, PromoteInputMode_DeclineAfterOnePromotion below would "pass" even
            // against the bug it's meant to catch, since the snapshot would never actually be
            // taken either way.
            _marketManager.MarketRow.Returns(new List<Card>());
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
            Assert.IsTrue(_turnContext.CanDeclineRemainingPromotions,
                "\"Promote UP TO 2\" credits must be voluntarily declinable - unlike a plain, mandatory \"promote a card played this turn\" (core_noble).");
        }

        // Drives the real ActionSystem/PromoteInputMode/PlayerStateManager path end to end (not
        // a mocked ActionSystem - calling a mock's CancelTargeting()/DeclineRemainingPromotions()
        // doesn't mutate anything, so a mocked test can't tell a correct "keep progress" decline
        // apart from a buggy "revert everything" one). Proves declining the remainder of a
        // multi-credit redemption preserves whatever was already promoted earlier in the same
        // session.
        [TestMethod]
        public void PromoteInputMode_DeclineAfterOnePromotion_KeepsTheEarlierPromotionInsteadOfRevertingIt()
        {
            var cardA = new Card("card_a", "Card A", 1, CardAspect.Neutral, 1, 1, 0);
            var cardB = new Card("card_b", "Card B", 1, CardAspect.Neutral, 1, 1, 0);
            _p1.AddToPlayed(cardA);
            _p1.AddToPlayed(cardB);
            _turnContext.AddPromotionCredit(cardA, 1, isOptional: true);
            _turnContext.AddPromotionCredit(cardB, 1, isOptional: true);

            // Mirrors GameplayState.SwitchToPromoteMode's real snapshot timing - taken BEFORE
            // any promotion happens in this redemption, exactly like the real client flow.
            _actionSystem.StartTargeting(ActionState.SelectingCardToPromote, null);

            var stateFake = new TestGameplayState
            {
                MatchContext = _context,
                TurnManager = _turnManager,
                ActionSystem = _actionSystem,
                MarketManager = _marketManager,
                MapManager = _mapManager,
                HoveredPlayedCard = cardA
            };
            var inputMode = new PromoteInputMode(stateFake, Substitute.For<IInputManager>(), _actionSystem, 2);

            // Left-click promotes cardA for real.
            var leftClick = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);
            inputMode.HandleInteraction(leftClick, _marketManager, _mapManager, _p1, _actionSystem);

            Assert.IsFalse(_p1.PlayedCards.Contains(cardA), "Setup check: cardA should be really promoted now.");
            Assert.Contains(cardA, _p1.InnerCircle, "Setup check: cardA should really be in the Inner Circle now.");

            // Right-click declines the remaining credit (cardB's).
            var rightClick = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);
            var result = inputMode.HandleInteraction(rightClick, _marketManager, _mapManager, _p1, _actionSystem);

            Assert.IsInstanceOfType(result, typeof(EndTurnCommand));
            Assert.IsFalse(_p1.PlayedCards.Contains(cardA), "The EARLIER promotion of cardA must survive - declining the rest must not revert it.");
            Assert.Contains(cardA, _p1.InnerCircle, "cardA must still be in the Inner Circle after declining the remaining credit.");
            Assert.Contains(cardB, _p1.PlayedCards, "cardB was never promoted - untouched by the decline.");
            Assert.AreEqual(0, _turnContext.PendingPromotionsCount, "The declined credit must be forfeited, not left dangling.");
        }

        // Same real-pipeline shape as above, but consuming EVERY credit normally (no decline) -
        // core_noble's plain, single, mandatory "promote a card played this turn" shape, the
        // most basic use of the deferred Promote flow. Proves the LAST left-click's own
        // "all done" completion path also preserves every promotion made in the session,
        // including cards promoted by earlier left-clicks in the same redemption.
        [TestMethod]
        public void PromoteInputMode_PromotingAllCreditsNormally_KeepsEveryPromotionMadeThisSession()
        {
            var cardA = new Card("card_a", "Card A", 1, CardAspect.Neutral, 1, 1, 0);
            var cardB = new Card("card_b", "Card B", 1, CardAspect.Neutral, 1, 1, 0);
            _p1.AddToPlayed(cardA);
            _p1.AddToPlayed(cardB);
            _turnContext.AddPromotionCredit(cardA, 1); // Plain, mandatory - core_noble's shape.
            _turnContext.AddPromotionCredit(cardB, 1);

            _actionSystem.StartTargeting(ActionState.SelectingCardToPromote, null);

            var stateFake = new TestGameplayState
            {
                MatchContext = _context,
                TurnManager = _turnManager,
                ActionSystem = _actionSystem,
                MarketManager = _marketManager,
                MapManager = _mapManager,
                HoveredPlayedCard = cardA
            };
            var inputMode = new PromoteInputMode(stateFake, Substitute.For<IInputManager>(), _actionSystem, 2);
            var leftClick = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            inputMode.HandleInteraction(leftClick, _marketManager, _mapManager, _p1, _actionSystem);
            Assert.Contains(cardA, _p1.InnerCircle, "Setup check: cardA should really be promoted now.");

            // Second (and last) click - hits HandleLeftClick's own "_cardsLeftToPromote <= 0"
            // completion branch.
            stateFake.HoveredPlayedCard = cardB;
            var result = inputMode.HandleInteraction(leftClick, _marketManager, _mapManager, _p1, _actionSystem);

            Assert.IsInstanceOfType(result, typeof(EndTurnCommand));
            Assert.Contains(cardA, _p1.InnerCircle, "The FIRST promotion must survive the session's own natural completion.");
            Assert.Contains(cardB, _p1.InnerCircle, "The second (and last) promotion should also have taken effect.");
            Assert.IsEmpty(_p1.PlayedCards, "Both cards should have left the Played pile.");
        }

        private Card GetCultistCard()
        {
            var card = new Card("cultist_of_myrkul", "Cultist of Myrkul", 2, CardAspect.Oblivion, 1, 2, 0);

            var devour = new CardEffect(EffectType.Devour, 1)
            {
                TargetLocation = CardLocation.Self,
                IsOptional = true,
                OnSuccess = new CardEffect(EffectType.Promote, 2) { PromotionCreditIsOptional = true },
                Alternative = new CardEffect(EffectType.GainResource, 2, ResourceType.Influence)
            };
            card.AddEffect(devour);

            return card;
        }
    }
}
