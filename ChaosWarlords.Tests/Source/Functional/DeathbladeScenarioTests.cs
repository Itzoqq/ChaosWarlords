using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Deathblade ("Assassinate 2 troops.") - the first
    /// shipped card using CardEffect's repeat mechanism (IEffectStrategy.SupportsRepeat +
    /// CardEffect.Amount as "how many separate targets", ActionExecutionEngine.
    /// ResolveCurrentEffect keeping the same EffectContext on the stack across all of them
    /// instead of popping after the first). Loads the REAL "deathblade" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DeathbladeScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with at least 2 empty neighbors, then marks 2 of those
        /// neighbors as Blue-occupied troop spaces - Red has Presence at both via the deployed
        /// troop's adjacency, matching this file's other setup helpers.
        /// </summary>
        private static (Player red, MapNode target1, MapNode target2) SetupRedWithTwoAdjacentEnemyTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Count(neighbor => neighbor.Occupant == PlayerColor.None) >= 2);
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));

            var emptyNeighbors = redNode.Neighbors.Where(n => n.Occupant == PlayerColor.None).Take(2).ToList();
            emptyNeighbors[0].Occupant = PlayerColor.Blue; // Setup only - not going through a command.
            emptyNeighbors[1].Occupant = PlayerColor.Blue;

            return (red, emptyNeighbors[0], emptyNeighbors[1]);
        }

        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayDeathblade_WithTwoValidTargets_AssassinatesBothWithoutSpendingPower()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");
            int powerBefore = red.Power;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target1, null);
            Assert.AreEqual(PlayerColor.None, target1.Occupant, "The first troop should already be assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "One more target is still owed - the state must not have advanced past Assassinate yet.");
            Assert.IsNotEmpty(scenario.Context.ActionSystem.ExecutionStack, "The Assassinate effect must still be on the stack, waiting for the 2nd target.");

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(PlayerColor.None, target2.Occupant, "The second troop should also be assassinated.");
            Assert.AreEqual(2, red.TrophyHall, "Both troops should be in the trophy hall.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
            Assert.AreEqual(powerBefore, red.Power, "Both assassinations are card-funded - neither should spend Power.");
        }

        // --- Row 3: no-valid-target fallback, plus the repeat-specific "fewer than requested
        // targets exist" edge case ---

        [TestMethod]
        public void PlayDeathblade_ExactlyOneEnemyTroopReachable_AssassinatesItAndResolvesWithoutAsecondClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var onlyTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            onlyTarget.Occupant = PlayerColor.Blue; // The ONLY enemy troop anywhere on the board.

            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(onlyTarget, null);

            Assert.AreEqual(PlayerColor.None, onlyTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "Only 1 troop existed to assassinate - the effect must not demand an impossible 2nd.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "With no more valid targets, the effect must resolve instead of leaving the player stuck.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayDeathblade_NoTroopsAnywhereOnTheBoard_SkipsAssassinateEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid targets anywhere means the effect should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.IsEmpty(scenario.Interactions, "No optional-effect popup exists on this card.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayDeathbladeCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "deathblade");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Deathblade should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/already-moved target for the SECOND repeat specifically ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForTheSecondDeathbladeTarget()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Still 1 more target owed.");

            // target1 is now an empty node (already assassinated) - re-targeting it for the
            // SECOND repeat must be rejected exactly like any other empty node would be.
            var forgedCommand = new AssassinateCommand(target1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-assassinated (now empty) node must be rejected as a target for the 2nd repeat.");
            Assert.AreEqual(1, red.TrophyHall, "The rejected re-target must not grant a second trophy.");
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileDeathbladeEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7/8: double-dispatch/replay and rapid dispatch while still validly targetable ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new AssassinateCommand(target1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "target1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed - exactly 1 more target should still be owed.");
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant, "target2 must remain untouched by the rejected replay.");

            // Finish the sequence normally to prove the repeat counter wasn't corrupted by the
            // rejected replay attempt.
            scenario.ClickTarget(target2, null);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Edge case: cancelling mid-repeat must revert BOTH the pending sequence AND the
        // already-resolved first target, not just leave the second target un-picked. ---

        [TestMethod]
        public void CancelTargeting_MidwayThroughDeathbladeRepeat_RevertsTheAlreadyResolvedFirstAssassinationToo()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "deathblade");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(1, red.TrophyHall, "Setup check: the first assassination resolved.");

            scenario.Context.ActionSystem.CancelTargeting();

            Assert.AreEqual(0, red.TrophyHall, "Cancelling mid-repeat must revert the whole card play, including the already-resolved first target - not just stop asking for the second.");
            Assert.AreEqual(PlayerColor.Blue, target1.Occupant, "target1's troop must be restored - the assassination must be fully undone.");
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant, "target2 was never touched.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            // By DefinitionId, not object reference or Card.Id: the snapshot restore rebuilds
            // Hand wholesale via CardFactory, which regenerates Card.Id on every restore - only
            // DefinitionId/RuntimeId survive unchanged (see CraniumRatsScenarioTests.cs for the
            // same established pattern).
            Assert.Contains(card.DefinitionId, red.Hand.Select(c => c.DefinitionId).ToList(), "The card itself should be restored to hand on cancel.");
        }
    }
}
