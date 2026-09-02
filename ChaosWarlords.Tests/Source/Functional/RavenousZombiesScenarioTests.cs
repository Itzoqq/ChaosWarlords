using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario coverage for Ravenous Zombies ("+1 Power. Assassinate a white troop.") - the
    /// first shipped card using CardEffect.TargetNeutralTroopOnly. Same shape as
    /// WightScenarioTests/CloakerScenarioTests: loads the REAL "ravenous_zombies" entry out of
    /// the REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    ///
    /// The core new behavior under test is that AssassinateCommand.Validate() independently
    /// re-derives the neutral-only restriction from ActionSystem.CurrentSourceEffect and
    /// enforces it server-side - a node occupied by an actual PLAYER'S troop (not Neutral) must
    /// be rejected even though it would be a perfectly legal target for an ordinary, unfiltered
    /// Assassinate (see AssassinateCommand.Validate/MapRuleEngine.CanAssassinate). The
    /// ActionInputController-level check (ActionInputController.HandleAssassinate) is only a
    /// UX convenience - it's covered separately/directly in ActionInputControllerTests.cs - the
    /// real defense exercised here is command Validate(), reached by dispatching a
    /// hand-constructed AssassinateCommand directly (bypassing ClickTarget/the input layer
    /// entirely), exactly the shape an untrusted client sending a forged command would produce.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class RavenousZombiesScenarioTests
    {
        /// <summary>
        /// Deploys Red at a real node and marks an adjacent node with <paramref name="occupant"/>
        /// - the shared setup every test below needs (Assassinate requires Presence, granted
        /// here via the deployed Red troop's adjacency).
        /// </summary>
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
        public void PlayRavenousZombies_WithNeutralTroopPresent_GainsPowerAndAssassinatesTheNeutralTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");

            scenario.PlayCard(card);

            // GainResource is pushed above Assassinate in the stack (LIFO of the written
            // order), so it resolves synchronously before targeting ever blocks - Power should
            // already be up even before the Assassinate click happens.
            Assert.AreEqual(1, red.Power, "The +1 Power half should resolve immediately, before the Assassinate click.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            Assert.IsNotNull(scenario.Context.ActionSystem.CurrentSourceEffect);
            Assert.IsTrue(scenario.Context.ActionSystem.CurrentSourceEffect!.TargetNeutralTroopOnly, "Ravenous Zombies' Assassinate effect must carry the neutral-only restriction.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant, "The Neutral troop should have been assassinated (node cleared).");
            Assert.AreEqual(1, red.TrophyHall, "Assassinate should have awarded a trophy.");
            Assert.AreEqual(1, red.Power, "Power should not have changed again during the Assassinate half.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayRavenousZombies_OnlyEnemyTroopsReachable_SkipsAssassinateButStillGrantsPower()
        {
            // No Neutral troop anywhere reachable (only an enemy-colored one) - the Assassinate
            // half's pre-push HasValidTargets lookahead (CardEffectProcessor.PushEffectContext)
            // must skip it gracefully: no exception, no stuck ActionState, and the Power gain
            // (an unconditional, unrelated effect) still happens.
            var scenario = MatchScenario.Build();
            var (red, enemyTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Power, "The +1 Power half must still resolve even though Assassinate has no valid target.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Neutral target means the card should fully resolve, not sit blocked on impossible targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant, "The enemy troop must survive untouched - it was never a legal target for this card.");
            Assert.AreEqual(0, red.TrophyHall);
        }

        [TestMethod]
        public void PlayRavenousZombies_NoTroopsAnywhereOnTheBoard_SkipsAssassinateButStillGrantsPower()
        {
            // Even more degenerate case: literally no troops deployed anywhere (not even Red's
            // own), so there's nothing to have Presence over in the first place.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Power);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayRavenousZombiesCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "ravenous_zombies"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Ravenous Zombies should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: THE CORE NEW BEHAVIOR - illegal filtered target rejected server-side ---

        [TestMethod]
        public void AssassinateCommand_TargetingAnActualPlayersTroop_IsRejectedWhileRavenousZombiesEffectIsPending()
        {
            // A node occupied by an actual PLAYER's troop (Blue) is a perfectly legal target
            // for an ORDINARY, unfiltered Assassinate - MapRuleEngine.CanAssassinate would
            // accept it with requireNeutralTroop: false. This is the one test that would have
            // caught a regression/omission in the TargetNeutralTroopOnly feature: with Ravenous
            // Zombies' Assassinate effect pending, Validate() must independently re-derive the
            // restriction from ActionSystem.CurrentSourceEffect and reject it anyway - not
            // trust the caller, and not rely solely on the input-layer (ActionInputController)
            // check which a forged command bypasses entirely.
            //
            // Needs a REAL Neutral troop present too (not just the Blue one) - otherwise the
            // pre-push HasValidTargets lookahead (CardEffectProcessor.PushEffectContext) skips
            // the Assassinate effect entirely before targeting even starts (see the
            // no-valid-target-fallback tests above), and this would never reach
            // TargetingAssassinate to prove anything. A 2-node site + a spy there grants Red
            // Presence at both without needing them to be map-adjacent to each other.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color); // Setup only - grants Presence at every node of this site.

            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "A real Neutral target exists, so targeting must have started.");

            var forgedCommand = new AssassinateCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected while TargetNeutralTroopOnly is in effect.");

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant, "Blue's troop must survive - it was never a legal target for this card's Assassinate.");
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileRavenousZombiesEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void AssassinateCommand_TargetingAnEmptyNode_IsRejectedWhileRavenousZombiesEffectIsPending()
        {
            // MapRuleEngine.CanAssassinate rejects target.Occupant == None before it ever
            // checks Presence, so this doesn't need to be adjacent to Red's troop at all - any
            // untouched node on the board is a valid "empty node" for this scenario.
            var scenario = MatchScenario.Build();
            var (_, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var emptyNode = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != neutralTarget);

            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(emptyNode.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An empty node (no troop at all) must be rejected.");
        }

        // --- Row 7/8: double-dispatch/replay and rapid dispatch while still validly targetable ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "ravenous_zombies");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new AssassinateCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The Neutral troop should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The stack should be fully resolved and back to Normal, not corrupted by the replayed dispatch.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
