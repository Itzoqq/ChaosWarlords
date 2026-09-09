using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Umber Hulk ("Deploy 3 troops. If an opponent causes you
    /// to discard this, they must discard a card.") - the second shipped Card.ReactiveDiscardEffect
    /// card (after Grimlock), and the first one that targets a SPECIFIC player rather than acting
    /// on its own owner: EffectType.ForceCausingOpponentDiscard resolves "the causing opponent" as
    /// TurnManager.CurrentTurnContext.ActivePlayer (the real turn-owner, which stays fixed even
    /// while TurnManager.ActivePlayer itself resolves to whoever's currently forced to discard),
    /// then queues them via MatchManager.EnqueueReactiveDiscard - reusing the exact same
    /// _pendingDiscardQueue/AdvanceOpponentDiscard machinery Neogi's end-of-turn "each opponent
    /// discards" phase already drives, gated by _discardPhaseEndsTurn so a mid-turn reactive
    /// phase doesn't also end the turn. Loads the REAL "umber_hulk" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher, mirroring
    /// GrimlockScenarioTests.cs's own structure (both Neogi's queue and Cranium Rats' ExecutionStack
    /// chain are exercised, since either can cause the initial discard).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class UmberHulkScenarioTests
    {
        // --- Row 1: positive/happy path for the PLAYED effect (unrelated to the reactive
        // trigger - Umber Hulk's "Deploy 3 troops" applies immediately like any other card). ---

        [TestMethod]
        public void PlayUmberHulk_GrantsThreeFreeTroopDeployments()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "umber_hulk");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.PendingFreeTroops);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- The core new behavior: forced-by-an-opponent discard reactively forces THAT
        // opponent to discard back, mid-turn, via Cranium Rats' ExecutionStack-based chain. ---

        [TestMethod]
        public void DiscardCommand_UmberHulkForcedByCraniumRats_ForcesCausingOpponentToDiscardBack()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            // Red needs a SPARE card left in hand after Cranium Rats is played away, to be able
            // to satisfy Umber Hulk's own reactive discard demand.
            scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            // Blue needs MORE than 3 cards (Cranium Rats' own threshold) to be an eligible
            // target - matches CraniumRatsScenarioTests.cs's own setup.
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var umberHulk = scenario.GiveCard(PlayerColor.Blue, "umber_hulk");

            scenario.PlayCard(craniumRats);
            int redHandSizeBeforeDiscard = red.Hand.Count;
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, umberHulk.Id));

            // Umber Hulk's own reactive trigger must now be demanding a discard from Red (the
            // one who played Cranium Rats and thus caused Blue's discard), not resolved yet.
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Umber Hulk's reactive discard should now be pending.");
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "Red - the causing opponent - must be the one now forced to discard.");

            var redCardToDiscard = red.Hand.First();
            scenario.Dispatch(new DiscardCardCommand(red.Color, redCardToDiscard.Id));

            Assert.DoesNotContain(redCardToDiscard, red.Hand);
            Assert.HasCount(redHandSizeBeforeDiscard - 1, red.Hand);
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "The reactive sequence should have completed normally.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "The forced-actor override should be fully released.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "It's still Red's own turn - Umber Hulk's reaction must not have ended it.");
            // NOTE: ActionSystem.CurrentState is deliberately NOT reset to Normal here, matching
            // NeogiScenarioTests.cs's own documented rationale - ResolveOpponentDiscard never
            // calls CompleteAction(), so CurrentState stays TargetingDiscard until the next real
            // targeting action overwrites it. IsResolvingOpponentDiscard/ForcedActingPlayer/
            // ActivePlayer above are the actual "sequence is done" signals.
        }

        [TestMethod]
        public void DiscardCommand_UmberHulkForcedByNeogi_MergesIntoTheSameEndOfTurnQueue()
        {
            // Regression: Umber Hulk's reactive discard must merge into an ALREADY-in-progress
            // Neogi end-of-turn phase rather than starting a second, conflicting one - and the
            // whole merged queue must still correctly end the turn once fully drained (not stop
            // early, and not skip ending the turn either).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var umberHulk = scenario.GiveCard(PlayerColor.Blue, "umber_hulk");

            scenario.Dispatch(new EndTurnCommand());
            // Captured AFTER EndTurnCommand, not before: MatchManager.EndTurn's own Cleanup/Draw
            // steps (2/3) refill Red's hand up to hand size BEFORE Blue's forced-discard phase
            // (3b) even begins, so Red already holds a fresh hand by the time its own reactive
            // discard demand arrives below.
            int redHandSizeBeforeDiscard = red.Hand.Count;
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "Setup check: Blue is now forced to discard by Neogi.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, umberHulk.Id));

            // Neogi's own queue had exactly 1 entry (Blue), so it's now empty, but Umber Hulk's
            // reactive trigger appended Red before that drain check ran - the phase must
            // continue into Red instead of ending the turn early.
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Umber Hulk's reactive discard should keep the phase going.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "Red must now be forced to discard reactively.");

            var redCardToDiscard = red.Hand.First();
            scenario.Dispatch(new DiscardCardCommand(red.Color, redCardToDiscard.Id));

            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
            Assert.HasCount(redHandSizeBeforeDiscard - 1, red.Hand);
            // NOTE: ActionSystem.CurrentState is deliberately NOT reset to Normal here - see
            // NeogiScenarioTests.cs's own documented rationale (ResolveOpponentDiscard never
            // calls CompleteAction()). IsResolvingOpponentDiscard/ActivePlayer below are the
            // actual "sequence is done, turn actually ended" signals.
            Assert.AreNotEqual(red, scenario.Context.ActivePlayer, "The turn should have actually ended and rotated to Blue now that the whole merged queue is drained.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer);
        }

        [TestMethod]
        public void DiscardCommand_UmberHulkDiscardedViaOwnEffect_DoesNotTriggerTheReactiveDiscard()
        {
            // Insane Outcast's own "discard a card from your hand" cost - the discarding
            // player's OWN choice, not an opponent's effect. The reactive trigger must not fire
            // even though the same DiscardCardCommand type is used.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var insaneOutcast = scenario.GiveCard(PlayerColor.Red, "insane_outcast");
            var umberHulk = scenario.GiveCard(PlayerColor.Red, "umber_hulk");
            scenario.PlayCard(insaneOutcast);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            scenario.Dispatch(new DiscardCardCommand(red.Color, umberHulk.Id));

            Assert.DoesNotContain(umberHulk, red.Hand);
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "No reactive discard should have been queued - this discard was self-caused, not an opponent's effect.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 4: wrong-player dispatch during Umber Hulk's reactive-forced discard window ---

        [TestMethod]
        public void DiscardCommand_DuringUmberHulksReactiveDiscard_DispatchedByTheWrongPlayer_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var umberHulk = scenario.GiveCard(PlayerColor.Blue, "umber_hulk");

            scenario.PlayCard(craniumRats);
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));
            scenario.Dispatch(new DiscardCardCommand(blue.Color, umberHulk.Id));
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "Setup check: Red is the one reactively forced to discard.");

            // Blue (not the player currently forced to discard) tries to satisfy Red's
            // obligation using Red's own card id - must be rejected by Validate().
            var redCard = red.Hand.First();
            scenario.AssertRejected(new DiscardCardCommand(blue.Color, redCard.Id));

            Assert.Contains(redCard, red.Hand, "Red's card must still be in hand - the rejected command must not have executed.");
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Still mid-sequence, waiting for Red's real discard.");
        }

        // --- Row 7: double-dispatch/replay - the reactive discard must not double-fire either. ---

        [TestMethod]
        public void DiscardCommand_UmberHulkDispatchedTwice_SecondDispatchIsRejected_ReactiveDiscardFiresOnlyOnce()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var umberHulk = scenario.GiveCard(PlayerColor.Blue, "umber_hulk");

            scenario.PlayCard(craniumRats);
            int redHandSizeBeforeDiscard = red.Hand.Count;
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));

            scenario.DispatchTwice(new DiscardCardCommand(blue.Color, umberHulk.Id));

            // Blue's own discard of Umber Hulk must not have applied twice, and exactly one
            // reactive discard demand on Red must now be pending (not two).
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
            Assert.AreEqual(red, scenario.Context.ActivePlayer);

            var redCardToDiscard = red.Hand.First();
            scenario.Dispatch(new DiscardCardCommand(red.Color, redCardToDiscard.Id));

            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Exactly one reactive discard should have been owed, not two.");
            Assert.HasCount(redHandSizeBeforeDiscard - 1, red.Hand);
        }

        // --- Regression, mirroring NeogiScenarioTests.CancelTargeting_MidQueue_
        // DoesNotDesyncNeogisForcedDiscardQueue: a generic ActionSystem cancel must not desync
        // the reactive queue either, now that ActionSystem.
        // ReleaseForcedActingPlayerIfOwnedByExecutionStack actively resumes it (via
        // MatchManager.ResumeReactiveDiscardQueue) instead of merely skipping the release. ---

        [TestMethod]
        public void CancelTargeting_MidReactiveDiscard_DoesNotDesyncTheQueue_AndSequenceStillCompletes()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var umberHulk = scenario.GiveCard(PlayerColor.Blue, "umber_hulk");

            scenario.PlayCard(craniumRats);
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));
            scenario.Dispatch(new DiscardCardCommand(blue.Color, umberHulk.Id));
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Setup check: Red's reactive discard should now be pending.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer);

            // Simulates the right-click-cancel path (PlayerController.HandleGlobalInput) firing
            // while the reactive discard prompt is open.
            scenario.Context.ActionSystem.CancelTargeting();

            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "The reactive queue must NOT have been silently abandoned by a generic ActionSystem cancel.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "ActivePlayer must still resolve to the queue's own pending entry.");
            // Re-entering TargetingDiscard (rather than leaving CurrentState at Normal while
            // ForcedActingPlayer stays overridden) is the self-consistent state - IsTargeting()
            // must still report true here, matching ActivePlayer/IsResolvingOpponentDiscard
            // above, not silently disagree with them.
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            // Bonus: prove the queue can still complete normally after surviving the cancel -
            // re-fetch from the CURRENT hand rather than a pre-cancel Card reference, matching
            // NeogiScenarioTests' own documented reasoning (CancelTargeting's snapshot-restore
            // branch rebuilds hands via fresh ICardDatabase.GetCardById lookups).
            var redCardToDiscard = red.Hand.First();
            scenario.Dispatch(new DiscardCardCommand(red.Color, redCardToDiscard.Id));

            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "The reactive sequence should have completed normally after surviving the cancel.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
        }
    }
}
