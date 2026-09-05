using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Grimlock ("Deploy a troop. If an opponent causes you
    /// to discard this, draw 2 cards.") - the first shipped card using Card.ReactiveDiscardEffect,
    /// the new "fires when THIS specific card is force-discarded by an opponent's effect"
    /// primitive (planning.txt's REACTIVE TRIGGERS item). Loads the REAL "grimlock" entry out
    /// of the REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    ///
    /// The reactive trigger only fires while TurnManager.ForcedActingPlayer is the discarding
    /// player - exactly "an opponent's effect caused this," as opposed to the card's own owner
    /// voluntarily discarding it (e.g. Insane Outcast's own-hand discard cost). Two shipped
    /// cards can force an opponent's discard via two independent mechanisms - Neogi (a
    /// cross-player queue in MatchManager) and Cranium Rats (a SelectOpponent-chained discard
    /// on ActionSystem's ExecutionStack) - both are exercised below, mirroring
    /// NeogiScenarioTests.cs's/CraniumRatsScenarioTests.cs's own setup patterns.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class GrimlockScenarioTests
    {
        // --- Row 1: positive/happy path for the PLAYED effect (unrelated to the reactive
        // trigger - Grimlock's "Deploy a troop" applies immediately like any other card). ---

        [TestMethod]
        public void PlayGrimlock_GrantsOneFreeTroopDeployment()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "grimlock");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.PendingFreeTroops);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- The core new behavior: forced-by-an-opponent discard triggers the reactive draw. ---

        [TestMethod]
        public void DiscardCommand_GrimlockForcedByOpponent_DrawsTwoExtraCards()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var grimlock = scenario.GiveCard(PlayerColor.Blue, "grimlock");
            int handSizeBeforeDiscard = blue.Hand.Count;

            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer);

            scenario.Dispatch(new DiscardCardCommand(blue.Color, grimlock.Id));

            Assert.DoesNotContain(grimlock, blue.Hand, "Grimlock itself should have been discarded.");
            Assert.Contains(grimlock, blue.DiscardPile, "Grimlock should be in the discard pile, not redirected anywhere.");
            Assert.HasCount(handSizeBeforeDiscard - 1 + 2, blue.Hand, "Losing Grimlock (-1) then drawing 2 from its reactive trigger should net +1 card in hand.");
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "The forced-discard sequence should have completed normally.");
        }

        [TestMethod]
        public void DiscardCommand_GrimlockForcedByCraniumRats_AlsoDrawsTwoExtraCards()
        {
            // Regression: Neogi's MatchManager._pendingDiscardQueue is NOT the only way a
            // shipped card forces an opponent to discard - Cranium Rats forces one via
            // SelectOpponent -> OnSuccess: DiscardCard, entirely on ActionSystem's
            // ExecutionStack, with MatchManager.IsResolvingOpponentDiscard staying false the
            // whole time. The reactive trigger must fire for this path too, not just Neogi's.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            // Blue needs MORE than 3 cards (Cranium Rats' own threshold) to be an eligible
            // target - matches CraniumRatsScenarioTests.cs's own setup.
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var grimlock = scenario.GiveCard(PlayerColor.Blue, "grimlock");
            int handSizeBeforeDiscard = blue.Hand.Count;

            scenario.PlayCard(craniumRats);
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Setup check: this is the ExecutionStack-based chain, not Neogi's queue.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, grimlock.Id));

            Assert.DoesNotContain(grimlock, blue.Hand);
            Assert.HasCount(handSizeBeforeDiscard - 1 + 2, blue.Hand, "The reactive draw must fire for Cranium Rats' forced discard too, not just Neogi's.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "The forced-actor override should still be fully released once the chain completes.");
        }

        [TestMethod]
        public void DiscardCommand_GrimlockDiscardedViaOwnEffect_DoesNotTriggerTheReactiveDraw()
        {
            // Insane Outcast's own "discard a card from your hand" cost - the discarding
            // player's OWN choice, not an opponent's effect. MatchManager.
            // IsResolvingOpponentDiscard is false for this path, so Grimlock's reactive trigger
            // must not fire even though the same DiscardCardCommand type is used.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var insaneOutcast = scenario.GiveCard(PlayerColor.Red, "insane_outcast");
            var grimlock = scenario.GiveCard(PlayerColor.Red, "grimlock");
            scenario.PlayCard(insaneOutcast);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);
            int handSizeBeforeDiscard = red.Hand.Count;

            scenario.Dispatch(new DiscardCardCommand(red.Color, grimlock.Id));

            Assert.DoesNotContain(grimlock, red.Hand);
            Assert.HasCount(handSizeBeforeDiscard - 1, red.Hand, "No reactive draw should have happened - this discard was self-caused, not an opponent's effect.");
        }

        // --- Row 4: wrong-player dispatch during the forced-discard window ---

        [TestMethod]
        public void DiscardCommand_DuringGrimlocksForcedDiscard_DispatchedByTheWrongPlayer_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var grimlock = scenario.GiveCard(PlayerColor.Blue, "grimlock");
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "Setup check: Blue is the one forced to discard, not Red.");

            // Red (not the player currently forced to discard) tries to satisfy Blue's
            // obligation using Blue's own card id - must be rejected by Validate().
            scenario.AssertRejected(new DiscardCardCommand(red.Color, grimlock.Id));

            Assert.Contains(grimlock, blue.Hand, "Grimlock must still be in Blue's hand - the rejected command must not have executed.");
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Still mid-sequence, waiting for Blue's real discard.");
        }

        // --- Row 7: double-dispatch/replay - the reactive draw must not double-fire either. ---

        [TestMethod]
        public void DiscardCommand_GrimlockDispatchedTwice_SecondDispatchIsRejected_ReactiveDrawFiresOnlyOnce()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var grimlock = scenario.GiveCard(PlayerColor.Blue, "grimlock");
            int handSizeBeforeDiscard = blue.Hand.Count;
            scenario.Dispatch(new EndTurnCommand());

            scenario.DispatchTwice(new DiscardCardCommand(blue.Color, grimlock.Id));

            Assert.HasCount(handSizeBeforeDiscard - 1 + 2, blue.Hand, "The reactive draw must have applied exactly once, not twice.");
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
        }
    }
}
