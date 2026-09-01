using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Cranium Rats (planning.txt TIER 2 #6) - "Deploy 2 troops;
    /// choose ONE opponent with more than 3 cards to discard a card." First shipped use of
    /// EffectType.SelectOpponent/SelectOpponentCommand/ActionState.TargetingOpponentSelect, the
    /// first "target a player" primitive in the codebase. Runs the full TIER 1 test matrix
    /// (planning.txt section 6.D) via the REAL "cranium_rats" cards.json entry, mirroring
    /// NeogiScenarioTests.cs's style (the closest sibling - also a forced-discard flow built on
    /// TurnManager.ForcedActingPlayer) and WightScenarioTests.cs's wrong-player/double-dispatch
    /// idiom.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CraniumRatsScenarioTests
    {
        [TestMethod]
        public void PlayCraniumRats_ChooseEligibleOpponent_DiscardResolves_AndFullyReleasesTheForcedActor()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            // Blue needs MORE than 3 cards (the card's own threshold) to be an eligible target.
            var blueCard1 = scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");

            scenario.PlayCard(craniumRats);

            Assert.AreEqual(2, red.PendingFreeTroops, "Deploy 2 troops should apply immediately, same as Neogi's own base effect.");
            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState);

            scenario.Dispatch(new SelectOpponentCommand(blue.Color));

            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "ActivePlayer should resolve to the chosen opponent (ForcedActingPlayer) during their forced discard.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, blueCard1.Id));

            Assert.DoesNotContain(blueCard1, blue.Hand, "The chosen opponent's card should have left their hand.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Unlike Neogi's queue, this chain goes through CompleteAction()'s normal branch, so it settles back to Normal once fully resolved.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "The forced-actor override must be fully released once the chain completes.");
        }

        [TestMethod]
        public void PlayCraniumRats_NoOpponentAboveThreshold_SkipsSelectOpponentCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            // Exactly at the threshold (3 cards) - NOT eligible ("more than 3").
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");

            scenario.PlayCard(craniumRats);

            Assert.AreEqual(2, red.PendingFreeTroops, "Deploy 2 troops should still apply - only the SelectOpponent half is skipped.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No Alternative on this card - a fully ineligible opponent pool means SelectOpponent is skipped straight back to Normal, not a hang.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
            Assert.HasCount(3, blue.Hand, "Blue's hand must be untouched - nothing to discard was ever chosen.");
        }

        [TestMethod]
        public void SelectOpponentCommand_ForPlayerAtExactlyTheThreshold_IsRejected_EvenWhileWindowIsOpen()
        {
            // Boundary value: the threshold is exclusive ("more than 3 cards") - exactly 3
            // must NOT be a legal target, even while TargetingOpponentSelect is genuinely open
            // (i.e. this isn't just "nobody is eligible so targeting never starts" - see the
            // fallback test above). Needs a 3rd seat: with only 2 players, the ONE opponent
            // either opens the window (eligible) or doesn't (nobody to reject) - there's no way
            // to have the window open AND target an ineligible player without a second opponent
            // to be the eligible one instead.
            var scenario = MatchScenario.Build(playerColors: new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Orange });
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue); // Eligible - keeps the window open.
            var orange = scenario.Player(PlayerColor.Orange); // Exactly at the threshold - not eligible.

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var orangeCard1 = scenario.GiveCard(PlayerColor.Orange, "core_house_guard");
            scenario.GiveCard(PlayerColor.Orange, "core_house_guard");
            scenario.GiveCard(PlayerColor.Orange, "core_house_guard");

            scenario.PlayCard(craniumRats);
            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue's eligibility alone should have opened the window.");

            scenario.AssertRejected(new SelectOpponentCommand(orange.Color), "Exactly 3 cards - not eligible (\"more than 3\"), even though the window is genuinely open.");

            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState, "Still waiting for a valid choice.");
            Assert.Contains(orangeCard1, orange.Hand);
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
        }

        [TestMethod]
        public void SelectOpponentCommand_TargetingSelf_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            var blue = scenario.Player(PlayerColor.Blue);
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard"); // Blue eligible - real targeting window open below.

            scenario.PlayCard(craniumRats);
            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new SelectOpponentCommand(red.Color), "The active player cannot choose themself.");

            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real choice.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
        }

        [TestMethod]
        public void SelectOpponentCommand_ForAColorNotInTheMatch_IsRejected()
        {
            // "Stale/nonexistent target" (planning.txt matrix row 5): MatchScenario always
            // builds a 2-player (Red/Blue) match - Orange isn't a seated player at all, so
            // GetPlayerByColor(Orange) resolves to null, the same shape a forged/corrupted
            // command referencing a player who was never in the match would take.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");

            scenario.PlayCard(craniumRats);
            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new SelectOpponentCommand(PlayerColor.Orange));
        }

        [TestMethod]
        public void SelectOpponentCommand_DispatchedOutsideTargetingOpponentSelect_IsRejected()
        {
            // Before the card is even played - CurrentState is Normal, not TargetingOpponentSelect.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            scenario.AssertRejected(new SelectOpponentCommand(blue.Color));
        }

        [TestMethod]
        public void SelectOpponentCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay). By the time the exact same command
            // instance is re-sent, CurrentState has already moved on to TargetingDiscard (the
            // chosen opponent's own discard step) - Validate() naturally rejects it without
            // needing any special-cased "already resolved" bookkeeping.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.PlayCard(craniumRats);

            scenario.DispatchTwice(new SelectOpponentCommand(blue.Color));

            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "The second dispatch must not have re-advanced or otherwise disturbed the sequence.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer);
        }

        [TestMethod]
        public void CancelTargeting_DuringOpponentDiscard_ReleasesTheForcedActor_AndReturnsTheCardToHand()
        {
            // The one genuinely new failure-mode surface this feature could introduce: a
            // stuck ForcedActingPlayer (and therefore a stuck ActivePlayer) after a cancel
            // mid-sequence - see ActionSystem.CancelTargeting's own new doc comment. Exercises
            // that fix directly, via the real ActionSystem the scenario is wired with.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.PlayCard(craniumRats);
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: mid-sequence, waiting on Blue's own discard.");
            Assert.IsNotNull(scenario.Context.TurnManager.ForcedActingPlayer);

            scenario.Context.ActionSystem.CancelTargeting();

            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "CancelTargeting must release the forced-actor override, not leave ActivePlayer stuck on the chosen opponent.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "ActivePlayer should have reverted to the real active player.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.Contains(craniumRats, red.Hand, "The played card itself is restored to hand on any cancel, by id, regardless of the snapshot mechanism below (see ActionSystem.TryRestoreCardToHand).");

            // NOT reverted, and this is a REAL, VERIFIED pre-existing gap surfaced by this
            // card (not something to silently assert-away): ActionSystem's full-state
            // snapshot/restore only fires for a targeting sequence started via the *full*
            // StartTargeting(...) method (see its own doc comment - "captured only when
            // actually leaving Normal state"). The mandatory-effect stack path a real
            // PlayCardCommand->CommandDispatcher play actually uses
            // (CardEffectProcessor.ResolveEffects -> ActionExecutionEngine.
            // HandleInputRequiredEffect -> SetupTargetingForRequiredEffect) transitions via the
            // bare IActionSystem.EnterTargetingState instead, which does NOT take a snapshot.
            // So CancelTargeting() falls back to its field-by-field ClearState() here, which
            // only clears ActionSystem's own Pending*/CurrentState - it has no way to know
            // GainResource(Troops) already ran for this same card earlier in the same
            // sequence. Net effect: cancelling Cranium Rats mid-sequence correctly returns the
            // card to hand and releases the forced actor, but silently keeps the 2 free troops
            // already granted. Asserted here as observed, real behavior - reported separately
            // as a production finding rather than "fixed" by this test (see delegator report).
            Assert.AreEqual(2, red.PendingFreeTroops, "KNOWN GAP (see comment above): the 2 troops from GainResource are NOT reverted by this cancel, because no full-state snapshot was ever taken for this targeting sequence.");
        }

        [TestMethod]
        public void PlayCraniumRats_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Blue, "cranium_rats"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(craniumRats));

            Assert.Contains(craniumRats, blue.Hand, "Cranium Rats should still be in Blue's hand - the command must not have executed.");
        }
    }
}
