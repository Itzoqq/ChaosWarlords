using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for DiscardCardCommand - added as TIER 1 item 2
    // (planning.txt, test-hardening audit, 2026-09-01): every other command already has a
    // dedicated file matching this pattern, but the 3 commands added alongside Neogi/Cloaker/
    // Ulitharid never got one, only scenario-level coverage via MatchScenario.
    [TestClass]
    [TestCategory("Unit")]
    public class DiscardCardCommandTests
    {
        private TestGameplayState _state = null!;
        private ChaosWarlords.Source.Entities.Actors.Player _player = null!;
        private Card _card = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _player = TestData.Players.RedPlayer();
            _card = TestData.Cards.CheapCard();
            _player.AddToHand(_card);

            _state.TurnManager.GetPlayerByColor(PlayerColor.Red).Returns(_player);
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenPlayerNotFound()
        {
            _state.TurnManager.GetPlayerByColor(PlayerColor.Blue).Returns((ChaosWarlords.Source.Entities.Actors.Player?)null);
            var command = new DiscardCardCommand(PlayerColor.Blue, _card.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenCardNotInPlayersHand()
        {
            // ActivePlayer must be stubbed to the same player so this genuinely exercises the
            // card-lookup branch, not incidentally pass via the (unrelated) ActivePlayer-
            // mismatch check added 2026-09-01.
            _state.TurnManager.ActivePlayer.Returns(_player);
            var command = new DiscardCardCommand(PlayerColor.Red, "card_that_does_not_exist");

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenCardInPlayersHand()
        {
            // Must also be ActivePlayer - see Validate_ReturnsFalse_WhenOwnerIsNotActivePlayer
            // below for the new (2026-09-01) branch that requires this.
            _state.TurnManager.ActivePlayer.Returns(_player);
            var command = new DiscardCardCommand(PlayerColor.Red, _card.Id);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenOwnerIsNotActivePlayer()
        {
            // Finding C (council-review 2026-09-01): the named player can genuinely own the
            // named card while someone ELSE is ActivePlayer (e.g. the real active player
            // trying to satisfy a forced opponent's discard requirement with their own card).
            // DiscardCardCommand.Validate() must reject this even though the ownership check
            // alone would pass.
            var otherActivePlayer = TestData.Players.BluePlayer();
            _state.TurnManager.ActivePlayer.Returns(otherActivePlayer);
            var command = new DiscardCardCommand(PlayerColor.Red, _card.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_DiscardsTheCard_FromHandToDiscardPile()
        {
            var command = new DiscardCardCommand(PlayerColor.Red, _card.Id);

            command.Execute(_state.MatchContext);

            Assert.DoesNotContain(_card, _player.Hand);
            Assert.Contains(_card, _player.DiscardPile);
        }

        [TestMethod]
        public void Execute_DoesNothing_WhenPlayerNotFound()
        {
            _state.TurnManager.GetPlayerByColor(PlayerColor.Blue).Returns((ChaosWarlords.Source.Entities.Actors.Player?)null);
            var command = new DiscardCardCommand(PlayerColor.Blue, _card.Id);

            command.Execute(_state.MatchContext);

            Assert.Contains(_card, _player.Hand, "Nothing should have moved - the target player couldn't be resolved.");
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_DoesNothing_WhenCardNotFound()
        {
            var command = new DiscardCardCommand(PlayerColor.Red, "card_that_does_not_exist");

            command.Execute(_state.MatchContext);

            Assert.Contains(_card, _player.Hand, "The real hand card must be untouched.");
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_NotResolvingOpponentDiscard_CallsActionSystemCompleteAction()
        {
            // Normal chain-continuation path (e.g. Insane Outcast's own "discard -> devour
            // self" chain) - the DiscardCard EffectContext is genuinely sitting on
            // ExecutionStack, so CompleteAction() resolves it.
            _state.MatchManager.IsResolvingOpponentDiscard.Returns(false);
            var command = new DiscardCardCommand(PlayerColor.Red, _card.Id);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.Received(1).CompleteAction();
            _state.MatchManager.DidNotReceive().ResolveOpponentDiscard(Arg.Any<Card>());
        }

        [TestMethod]
        public void Execute_ResolvingOpponentDiscard_CallsMatchManagerResolveOpponentDiscard_NotCompleteAction()
        {
            // Neogi's cross-player forced-discard sequence in progress - this discard has
            // NOTHING on ActionSystem's ExecutionStack (MarkOpponentDiscardAtEndOfTurn already
            // resolved, long ago, during Neogi's own play), so CompleteAction() would hit its
            // no-stack-context fallback and incorrectly reset CurrentState after just one
            // opponent. Must advance the sequence instead.
            _state.MatchManager.IsResolvingOpponentDiscard.Returns(true);
            var command = new DiscardCardCommand(PlayerColor.Red, _card.Id);

            command.Execute(_state.MatchContext);

            _state.MatchManager.Received(1).ResolveOpponentDiscard(_card);
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        // NOTE (2026-09-01, council-review fix chain for f4f2de1/Cranium Rats): this file used
        // to have 3 tests here (Execute_NormalPath_StateBackToNormalWithForcedActingPlayerSet_
        // ReleasesTheOverride / ..._NoForcedActingPlayerSet_DoesNotCallEndForcedActingPlayer /
        // ..._ChainStillInProgress_DoesNotCallEndForcedActingPlayer) asserting that
        // DiscardCardCommand.Execute() itself called/didn't call TurnManager.
        // EndForcedActingPlayer() depending on ActionSystem.CurrentState. That release is no
        // longer DiscardCardCommand's responsibility at all - it moved to a generic
        // ActionSystem.ReleaseForcedActingPlayerIfOwnedByExecutionStack() helper invoked from
        // ClearState()/CancelTargeting(), which a mocked ActionSystem (as used in this file)
        // never actually invokes. Removed rather than kept as now-vacuous assertions (Execute()
        // never calls EndForcedActingPlayer regardless of CurrentState any more, so the
        // DidNotReceive() cases would pass for the wrong reason). The real behavior these tests
        // covered is now exercised at the scenario level, through the REAL ActionSystem, in
        // CraniumRatsScenarioTests.CancelTargeting_DuringOpponentDiscard_ReleasesTheForcedActor_
        // AndReturnsTheCardToHand and NeogiScenarioTests'
        // CancelTargeting_DuringNeogiOpponentDiscardQueue_DoesNotDesyncTheQueue (the actual bug
        // this fix closes) - see RESOLVED.txt.
    }
}
