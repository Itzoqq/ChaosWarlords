using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Advance Scout ("Supplant a white troop.") - the
    /// simplest of the 7 cards shipped alongside CardEffect.IgnoresPresenceRequirement
    /// (2026-09-03): a single, unconditional, non-optional Supplant with
    /// TargetNeutralTroopOnly and normal Presence still required (no IgnoresPresenceRequirement
    /// on this card). Same shape/rigor as RavenousZombiesScenarioTests, but for Supplant
    /// instead of Assassinate - loads the REAL "advance_scout" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class AdvanceScoutScenarioTests
    {
        private static (Player red, MapNode targetNode) SetupRedWithAdjacentTroop(MatchScenario scenario, PlayerColor occupant)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var targetNode = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            targetNode.Occupant = occupant; // Setup only - not going through a command.

            return (red, targetNode);
        }

        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayAdvanceScout_WithNeutralTroopPresent_SupplantsTheNeutralTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
            Assert.IsNotNull(scenario.Context.ActionSystem.CurrentSourceEffect);
            Assert.IsTrue(scenario.Context.ActionSystem.CurrentSourceEffect!.TargetNeutralTroopOnly, "Advance Scout's Supplant effect must carry the neutral-only restriction.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant, "Red's troop should have Supplanted the Neutral one.");
            Assert.AreEqual(1, red.TrophyHall, "Supplant's assassinate half should award a trophy.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayAdvanceScout_OnlyEnemyTroopsReachable_SkipsSupplantEntirely()
        {
            var scenario = MatchScenario.Build();
            var (red, enemyTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Neutral target means the card should fully resolve, not sit blocked on impossible targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant, "The enemy troop must survive untouched - it was never a legal target for this card.");
            Assert.AreEqual(0, red.TrophyHall);
        }

        [TestMethod]
        public void PlayAdvanceScout_NoTroopsAnywhereOnTheBoard_SkipsSupplantEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayAdvanceScoutCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "advance_scout");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Advance Scout should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/illegal SupplantCommand targets, rejected server-side ---

        [TestMethod]
        public void SupplantCommand_TargetingAnActualPlayersTroop_IsRejectedWhileAdvanceScoutEffectIsPending()
        {
            // Mirrors RavenousZombiesScenarioTests' equivalent Assassinate test: a node
            // occupied by an actual PLAYER's troop is a perfectly legal target for an
            // ORDINARY, unfiltered Supplant - Validate() must independently re-derive the
            // neutral-only restriction from ActionSystem.CurrentSourceEffect and reject it
            // anyway. Needs a real Neutral troop too, or the pre-push HasValidTargets
            // lookahead skips Supplant entirely before targeting starts.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color); // Setup only - grants Presence at every node of this site.

            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "A real Neutral target exists, so targeting must have started.");

            var forgedCommand = new SupplantCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected while TargetNeutralTroopOnly is in effect.");

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant, "Blue's troop must survive - it was never a legal target for this card's Supplant.");
        }

        [TestMethod]
        public void SupplantCommand_TargetingANonexistentNode_IsRejectedWhileAdvanceScoutEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void SupplantCommand_TargetingAnEmptyNode_IsRejectedWhileAdvanceScoutEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var (_, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var emptyNode = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != neutralTarget);

            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(emptyNode.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An empty node (no troop at all) must be rejected.");
        }

        // --- Row 7/8: double-dispatch/replay and rapid dispatch while still validly targetable ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "advance_scout");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The Neutral troop should have been Supplanted exactly once, not twice.");
            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The stack should be fully resolved and back to Normal, not corrupted by the replayed dispatch.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
