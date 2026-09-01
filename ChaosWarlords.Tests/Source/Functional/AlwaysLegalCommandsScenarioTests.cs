using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Spam/idempotency coverage for the always-legal, no-target commands (see planning.txt
    /// TIER 1 item 7 - Validate() always returns true for these, so nothing stops a client from
    /// dispatching them with nothing pending or firing the same one twice back-to-back; this
    /// was verified ZERO coverage anywhere in the suite before the test-hardening audit,
    /// section 6.C.3). Uses the real MatchScenario harness (not mocks) specifically because the
    /// risk here lives inside ActionSystem's/MatchManager's own real implementation when
    /// called with nothing pending - a mocked ActionSystem can't surface that, it would just
    /// record the call and trivially "pass".
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class AlwaysLegalCommandsScenarioTests
    {
        [TestMethod]
        public void CancelActionCommand_WithNothingPending_IsASafeNoOp()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            scenario.Dispatch(new CancelActionCommand()); // Must not throw.

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void CancelActionCommand_DispatchedTwiceBackToBack_IsSafe()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            scenario.Dispatch(new CancelActionCommand());
            scenario.Dispatch(new CancelActionCommand()); // Must not throw the second time either.

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void CancelActionCommand_DuringRealTargeting_CancelsCleanly()
        {
            // The more realistic "spam" shape: a player mashes Escape/right-click mid-targeting.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var wight = scenario.GiveCard(PlayerColor.Red, "wight");
            scenario.GiveCard(PlayerColor.Red, "core_noble"); // A real Devour target.

            // Wight's popup only fires if the FULL chain (Devour -> Supplant) has a valid
            // target, not just the Devour half - give it a real Supplant target too.
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            redNode.Neighbors.First(n => n.Occupant == PlayerColor.None).Occupant = blue.Color;

            scenario.PlayCard(wight);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingDevourHand, scenario.Context.ActionSystem.CurrentState);

            scenario.Dispatch(new CancelActionCommand());
            var stateAfterFirstCancel = scenario.Context.ActionSystem.CurrentState;
            var powerAfterFirstCancel = red.Power;
            var handCountAfterFirstCancel = red.Hand.Count;

            // The interesting "spam" property: cancelling a SECOND time, with nothing left to
            // cancel, must be a safe no-op - not throw, and not change anything further (e.g.
            // re-restoring an already-consumed snapshot, or double-crediting a fallback).
            // (SequenceNumber/GetStateHash aren't compared here - CancelActionCommand is
            // always-legal, so each dispatch legitimately advances SequenceNumber; that alone
            // would fail a full-hash comparison without indicating any real problem.)
            scenario.Dispatch(new CancelActionCommand());

            Assert.AreEqual(stateAfterFirstCancel, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(powerAfterFirstCancel, red.Power);
            Assert.HasCount(handCountAfterFirstCancel, red.Hand);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        [TestMethod]
        public void SwitchToNormalModeCommand_WithNothingPending_IsASafeNoOp()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            scenario.Dispatch(new SwitchToNormalModeCommand());
            scenario.Dispatch(new SwitchToNormalModeCommand());

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void ActionCompletedCommand_WithNothingPending_IsASafeNoOp()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            long sequenceBefore = scenario.Context.SequenceNumber;

            scenario.Dispatch(new ActionCompletedCommand());
            scenario.Dispatch(new ActionCompletedCommand());

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            // Unlike a rejected command, these are legitimately DISPATCHED (Validate() is
            // unconditionally true) - SequenceNumber advances for each even though Execute()
            // itself is a no-op marker.
            Assert.AreEqual(sequenceBefore + 2, scenario.Context.SequenceNumber);
        }

        [TestMethod]
        public void EndTurnCommand_DispatchedTwiceBackToBack_AdvancesRotationTwiceWithoutCorruption()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            scenario.Dispatch(new EndTurnCommand());
            var afterFirst = scenario.Context.ActivePlayer;
            Assert.AreNotEqual(red.Color, afterFirst.Color, "First EndTurn should rotate away from Red.");

            scenario.Dispatch(new EndTurnCommand()); // Must not throw or desync - a 2nd legitimate "pass" turn.
            var afterSecond = scenario.Context.ActivePlayer;

            Assert.AreEqual(red.Color, afterSecond.Color, "With only 2 players, two EndTurns in a row should land back on Red.");
        }
    }
}
