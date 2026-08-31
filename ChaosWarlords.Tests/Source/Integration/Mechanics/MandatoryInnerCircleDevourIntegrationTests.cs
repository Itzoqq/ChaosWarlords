using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Tests.Utilities;
using NSubstitute;
using System.Linq;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// No shipped card has a mandatory (non-optional) InnerCircle-targeted Devour effect
    /// today (Cultist of Myrkul's is optional) - this whole flow was previously untested and
    /// only reachable by hand-tracing the code. Written while fixing
    /// NormalPlayInputMode.ShouldHandleDevourPreCommit (see planning.txt): that method used
    /// to also pre-commit InnerCircle-targeted devour, which would have silently corrupted
    /// this exact flow (MatchManager.ShouldResumeDevourChain's "source card not on stack"
    /// fallback would resume the OnSuccess chain WITHOUT ever actually playing the card).
    /// This test proves the flow this fix now relies on - the EXISTING post-play
    /// required-input path, unmodified - already handles a mandatory InnerCircle devour
    /// correctly on its own, with no special-casing needed.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MandatoryInnerCircleDevourIntegrationTests
    {
        private ActionSystem _actionSystem = null!;
        private MatchManager _matchManager = null!;
        private MatchContext _context = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            TestLogger.Initialize();
            var logger = TestLogger.Instance;

            _player = new Player(PlayerColor.Red, displayName: "Player 1");
            var p2 = new Player(PlayerColor.Blue, displayName: "Player 2");

            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var cardDatabase = Substitute.For<ICardDatabase>();

            var turnManager = new TurnManager(
                new System.Collections.Generic.List<Player> { _player, p2 },
                new ChaosWarlords.Source.Core.Utilities.SeededGameRandom(12345, logger),
                logger);
            _player = turnManager.ActivePlayer; // TurnManager shuffles player order

            _actionSystem = new ActionSystem(turnManager, mapManager, logger);
            var playerState = new PlayerStateManager(logger);
            _actionSystem.SetPlayerStateManager(playerState);
            _actionSystem.SetMarketManager(marketManager);

            _context = new MatchContext(turnManager, mapManager, marketManager, _actionSystem, cardDatabase, playerState, logger);
            _actionSystem.SetMatchContext(_context);

            _matchManager = new MatchManager(_context, logger, Substitute.For<IVictoryManager>());
            _actionSystem.SetMatchManager(_matchManager);
        }

        private static Card MandatoryInnerCircleDevourCard()
        {
            var card = new Card("test_mandatory_ic_devour", "Test Mandatory IC Devour", 3, CardAspect.Oblivion, 1, 3, 0);
            card.Effects.Add(new CardEffect(EffectType.GainResource, 2, ResourceType.Power));
            var devour = new CardEffect(EffectType.Devour, 1) { TargetLocation = CardLocation.InnerCircle, IsOptional = false };
            devour.OnSuccess = new CardEffect(EffectType.GainResource, 3, ResourceType.Influence);
            card.Effects.Add(devour);
            return card;
        }

        [TestMethod]
        public void PlayingCard_EntersInnerCircleTargeting_WithSourceCardOnStack()
        {
            // Arrange
            var sourceCard = MandatoryInnerCircleDevourCard();
            sourceCard.Location = CardLocation.Hand;
            var innerCircleCard = new Card("ic_card", "IC Card", 1, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.InnerCircle };
            _player.AddToHand(sourceCard);
            _player.AddToInnerCircle(innerCircleCard);

            // Act - play the card via the normal (post-play) path, exactly as
            // NormalPlayInputMode does for any devour effect that isn't Hand-targeted.
            _matchManager.PlayCard(sourceCard);

            // Assert - the base effect already applied (it's processed before the blocking
            // Devour effect is reached)...
            Assert.AreEqual(2, _player.Power);

            // ...and the card correctly entered InnerCircle targeting, with the source card
            // genuinely pushed onto ExecutionStack by ResolveEffects (not the "direct API
            // call, no stack" case) - this is exactly what MatchManager.ShouldResumeDevourChain
            // checks for to avoid manually resuming the chain (and double-processing it) once
            // the target gets selected below.
            Assert.AreEqual(ActionState.TargetingDevourInnerCircle, _actionSystem.CurrentState);
            Assert.IsTrue(_actionSystem.ExecutionStack.Any(ctx => ctx.SourceCard == sourceCard),
                "Source card must be on ExecutionStack - required for ShouldResumeDevourChain's stack-based branch, not the manual-resume fallback.");
            Assert.AreEqual(CardLocation.Played, sourceCard.Location, "Card must already be Played, not stuck in Hand, by the time targeting starts.");
        }

        [TestMethod]
        public void SelectingInnerCircleTarget_CompletesTheWholeChain_ExactlyOnce()
        {
            // Arrange
            var sourceCard = MandatoryInnerCircleDevourCard();
            sourceCard.Location = CardLocation.Hand;
            var innerCircleCard = new Card("ic_card", "IC Card", 1, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.InnerCircle };
            var otherIcCard = new Card("other_ic_card", "Other IC Card", 1, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.InnerCircle };
            _player.AddToHand(sourceCard);
            _player.AddToInnerCircle(innerCircleCard);
            _player.AddToInnerCircle(otherIcCard);

            _matchManager.PlayCard(sourceCard);
            Assert.AreEqual(ActionState.TargetingDevourInnerCircle, _actionSystem.CurrentState);

            // Act - mirrors DevourInputMode.HandleCardClick's TargetingDevourInnerCircle
            // branch: HandleDevourInnerCircleSelection returns a command, which the real
            // input pipeline dispatches via RecordAndExecuteCommand.
            var cmd = _actionSystem.HandleDevourInnerCircleSelection(innerCircleCard);
            Assert.IsNotNull(cmd);
            cmd!.Execute(_context);

            // Assert - devoured target removed from Inner Circle exactly once, into VoidPile.
            CollectionAssert.DoesNotContain(_player.InnerCircle.ToList(), innerCircleCard);
            CollectionAssert.Contains(_player.InnerCircle.ToList(), otherIcCard, "Uninvolved Inner Circle card must be untouched.");
            CollectionAssert.Contains(_context.VoidPile.ToList(), innerCircleCard);
            Assert.AreEqual(1, _context.VoidPile.Count(c => c == innerCircleCard));

            // Base effect (2 Power) and OnSuccess effect (3 Influence) both applied exactly once.
            Assert.AreEqual(2, _player.Power);
            Assert.AreEqual(3, _player.Influence);

            // The card itself was actually played, not left stranded.
            Assert.AreEqual(CardLocation.Played, sourceCard.Location);
            CollectionAssert.Contains(_player.PlayedCards.ToList(), sourceCard);
            CollectionAssert.DoesNotContain(_player.Hand.ToList(), sourceCard);

            // Stack/state fully resolved - no leftover targeting mode bleeding into whatever
            // card gets played next this turn (see planning.txt's Supplant/ReturnTroop
            // stack-leak entries for the bug shape this guards against).
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
            Assert.IsEmpty(_actionSystem.ExecutionStack);
        }
    }
}
