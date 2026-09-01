using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Neogi (see planning.txt TIER 1 audit, 2026-09-01) - the
    /// largest single piece of new engine work this session (TurnManager.ForcedActingPlayer,
    /// MatchManager's interruptible EndTurn/opponent-discard-phase sequencing), and until now
    /// only ever exercised via NeogiMechanicsTests.cs's hand-typed card + direct
    /// command.Execute(context) calls. MatchFactory always builds exactly 2 players (Red/Blue)
    /// - NeogiMechanicsTests.cs's 3-player seat-order/stacking coverage is NOT duplicated here,
    /// this exists to prove the same sequencing survives the REAL PlayCardCommand/EndTurnCommand
    /// -> CommandDispatcher path with the REAL "neogi" cards.json entry.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class NeogiScenarioTests
    {
        [TestMethod]
        public void PlayNeogi_MarksPendingTrigger_DoesNotDiscardYet()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);

            Assert.HasCount(1, scenario.Context.PendingOpponentDiscardTriggers);
            Assert.AreEqual(4, red.PendingFreeTroops, "Deploy 4 troops should apply immediately.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Playing Neogi itself shouldn't demand anything - the discard is deferred to end of turn.");
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
        }

        [TestMethod]
        public void EndTurn_AfterNeogi_ForcesTheOpponentToDiscard_ThenCompletesNormalRotation()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var blueCard = scenario.GiveCard(PlayerColor.Blue, "core_noble");

            scenario.Dispatch(new EndTurnCommand());

            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "ActivePlayer should resolve to Blue (ForcedActingPlayer) during Blue's forced discard.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, blueCard.Id));

            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Sequence complete - the real end-of-turn player switch should now have happened.");
            Assert.DoesNotContain(blueCard, blue.Hand);
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "With only 2 players, normal rotation lands on Blue too - it's genuinely Blue's turn now, not still the forced override.");
            // NOTE: ActionSystem.CurrentState is deliberately NOT reset to Normal here -
            // ResolveOpponentDiscard never calls CompleteAction() (see its own doc comment:
            // doing so would hit the no-stack-context fallback and break sequencing after just
            // one opponent), so CurrentState stays TargetingDiscard until the next real
            // targeting action overwrites it. Not asserted on here - IsResolvingOpponentDiscard
            // and ActivePlayer are the actual "sequence is done" signals.
        }

        [TestMethod]
        public void EndTurn_OpponentWithEmptyHand_SkipsStraightToNormalRotation()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue); // Deliberately left with an empty hand.

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);

            scenario.Dispatch(new EndTurnCommand());

            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Blue should be skipped entirely (empty hand) - straight to normal rotation.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "Normal rotation still lands on Blue - it's just not a forced discard.");
        }

        [TestMethod]
        public void DiscardCardCommand_DuringOpponentDiscardPhase_ForANonexistentCard_IsRejectedWithNoStateChange()
        {
            // Adversarial scenario: a forged/stale DiscardCardCommand during Neogi's
            // cross-player forced-discard window, naming a card the target player doesn't
            // have, must be rejected by Validate() - not silently advance the sequence or
            // discard something else.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var blueCard = scenario.GiveCard(PlayerColor.Blue, "core_noble");
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DiscardCardCommand(blue.Color, "card_that_does_not_exist"));

            Assert.Contains(blueCard, blue.Hand, "The real hand card must be untouched.");
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Still mid-sequence, waiting for a real discard.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "ForcedActingPlayer override must not have been cleared by the rejected command.");
        }

        [TestMethod]
        public void PlayNeogiCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");

            scenario.DispatchTwice(new PlayCardCommand(neogi));

            Assert.AreEqual(4, red.PendingFreeTroops, "Deploy 4 troops should have applied exactly once, not twice.");
            Assert.HasCount(1, scenario.Context.PendingOpponentDiscardTriggers, "Only one Neogi's worth of forced-discard should be pending.");
        }
    }
}
