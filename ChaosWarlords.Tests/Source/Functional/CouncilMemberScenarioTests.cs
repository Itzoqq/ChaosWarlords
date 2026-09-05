using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Council Member ("Move up to 2 enemy troops. At end
    /// of turn, promote another card played this turn.") - the first shipped card using
    /// CardEffect.AllowPartialRepeat/DeclineRepeatCommand (a voluntary "stop early, keep
    /// whatever already resolved" repeat, distinct from Deathblade's mandatory "exactly N"
    /// repeat, which can only stop early via the board running out of legal targets) and the
    /// first to wire IEffectStrategy.SupportsRepeat for MoveUnit's 2-ActionState (source, then
    /// destination) targeting flow. Loads the REAL "council_member" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher. Row 6 (unmet
    /// resource precondition) doesn't apply - neither MoveTroopCommand nor the deferred
    /// Promote-credit flow this card produces has a resource cost of its own to fail. Row 8
    /// (rapid/back-to-back dispatch) is exercised implicitly by the happy-path test's two
    /// back-to-back legitimate repeats, matching DeathbladeScenarioTests' precedent.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CouncilMemberScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with >= 2 empty neighbors, marks 2 of those neighbors as
        /// Blue-occupied troop spaces (Red has Presence at both via the deployed troop's
        /// adjacency, matching DeathbladeScenarioTests' setup), and returns 2 further empty
        /// nodes - in a DIFFERENT site than Red's, so Red has no Presence there either - to use
        /// as move destinations that don't accidentally grant a 3rd legal move.
        /// </summary>
        private static (Player red, MapNode target1, MapNode target2, MapNode dest1, MapNode dest2) SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Count(neighbor => neighbor.Occupant == PlayerColor.None) >= 2);
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));

            var emptyNeighbors = redNode.Neighbors.Where(n => n.Occupant == PlayerColor.None).Take(2).ToList();
            emptyNeighbors[0].Occupant = PlayerColor.Blue; // Setup only - not going through a command.
            emptyNeighbors[1].Occupant = PlayerColor.Blue;

            var redSite = scenario.Context.MapManager.GetSiteForNode(redNode);
            var destinations = scenario.Context.MapManager.Nodes
                .Where(n => n.Occupant == PlayerColor.None
                    && scenario.Context.MapManager.GetSiteForNode(n) != redSite
                    && !redNode.Neighbors.Contains(n))
                .Take(2)
                .ToList();

            return (red, emptyNeighbors[0], emptyNeighbors[1], destinations[0], destinations[1]);
        }

        /// <summary>
        /// Deploys Red at a real node and marks one adjacent node as Blue-occupied - the
        /// minimal setup needed for a SINGLE valid MoveUnit target (see
        /// TrivialPrimitiveCardsScenarioTests' identically-shaped helper), used where a test
        /// only needs targeting to actually open, not a full 2-repeat sequence.
        /// </summary>
        private static Player SetupRedWithOneAdjacentEnemyTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = PlayerColor.Blue;
            return red;
        }

        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayCouncilMember_MovesBothEnemyTroopsAndBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var (_, target1, target2, dest1, dest2) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target1, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(dest1, null);
            Assert.AreEqual(PlayerColor.Blue, dest1.Occupant, "First troop should have moved.");
            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState,
                "One more repeat is still owed - must be back to picking a NEW source, not stuck on Destination.");
            Assert.IsNotEmpty(scenario.Context.ActionSystem.ExecutionStack, "The MoveUnit effect must still be on the stack, waiting for the 2nd repeat.");

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(dest2, null);
            Assert.AreEqual(PlayerColor.Blue, dest2.Occupant, "Second troop should have moved.");
            Assert.AreEqual(PlayerColor.None, target2.Occupant);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount,
                "The 2nd, independent top-level effect (Promote 1) should also have resolved, banking 1 credit.");
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayCouncilMember_NoEnemyTroopsAnywhere_SkipsMoveButStillBanksThePromotionCredit()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState,
                "No valid Move targets anywhere means that effect should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount,
                "The independent Promote effect must still resolve even though Move found nothing to do.");
        }

        [TestMethod]
        public void PlayCouncilMember_ExactlyOneEnemyTroopReachable_MovesItAndResolvesWithoutASecondClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var onlyTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            onlyTarget.Occupant = PlayerColor.Blue; // The ONLY enemy troop anywhere on the board.

            var redSite = scenario.Context.MapManager.GetSiteForNode(redNode);
            var destination = scenario.Context.MapManager.Nodes.First(n =>
                n.Occupant == PlayerColor.None
                && scenario.Context.MapManager.GetSiteForNode(n) != redSite
                && !redNode.Neighbors.Contains(n));

            var card = scenario.GiveCard(PlayerColor.Red, "council_member");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(onlyTarget, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(destination, null);

            Assert.AreEqual(PlayerColor.Blue, destination.Occupant);
            Assert.AreEqual(PlayerColor.None, onlyTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState,
                "Only 1 troop was ever reachable - the effect must not demand an impossible 2nd.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayCouncilMemberCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "council_member");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Council Member should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent/already-moved target for each targeting sub-step ---

        [TestMethod]
        public void MoveTroopCommand_TargetingTheAlreadyMovedNodeAsASource_IsRejectedForTheSecondRepeat()
        {
            var scenario = MatchScenario.Build();
            var (_, target1, target2, dest1, dest2) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            scenario.ClickTarget(dest1, null);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState, "Still 1 more repeat owed.");

            // target1 is now an empty node (already moved away) - re-targeting it as the
            // source for the SECOND repeat must be rejected exactly like any other empty node.
            var forgedCommand = new MoveTroopCommand(target1.Id, dest2.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-vacated node must be rejected as a move source for the 2nd repeat.");
            Assert.AreEqual(PlayerColor.None, dest2.Occupant, "The rejected re-target must not have moved anything.");

            // Finish the sequence normally to prove the repeat counter wasn't corrupted.
            scenario.ClickTarget(target2, null);
            scenario.ClickTarget(dest2, null);
            Assert.AreEqual(PlayerColor.Blue, dest2.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void MoveTroopCommand_TargetingANonexistentSourceNode_IsRejectedWhileCouncilMemberEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var (_, _, _, dest1, _) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new MoveTroopCommand(999999, dest1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent source node id must be rejected.");
        }

        [TestMethod]
        public void DeclineRepeatCommand_WithAMismatchedCardId_IsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DeclineRepeatCommand("some_other_card"), "A decline referencing the wrong card's sequence must be rejected.");
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState, "The rejected decline must not have touched the pending sequence.");
        }

        [TestMethod]
        public void DeclineRepeatCommand_WhileMidwayThroughASingleMove_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var (_, target1, _, _, _) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null); // Source picked - now mid-way to TargetingMoveDestination.
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DeclineRepeatCommand(card.Id), "Declining is only valid at a repeat boundary, not mid a single move's source/destination pair.");
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void DeclineRepeatCommand_OnACardWhoseEffectDoesNotAllowPartialRepeat_IsRejected()
        {
            // test_displacer's "Move an enemy troop" (Amount=1, no AllowPartialRepeat) is a
            // legitimate repeat-boundary state (TargetingMoveSource) but must never be
            // declinable - proves the gate is the AllowPartialRepeat flag, not just the state.
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_displacer");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DeclineRepeatCommand(card.Id));
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Voluntary partial decline (the primitive this card exists to exercise) ---

        [TestMethod]
        public void PlayCouncilMember_DeclineBeforeMovingAnyTroops_SkipsMoveWithZeroRepeatsUsed()
        {
            var scenario = MatchScenario.Build();
            var (_, target1, target2, _, _) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(PlayerColor.Blue, target1.Occupant, "Nothing should have moved - the player declined immediately.");
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount,
                "The independent Promote effect must still resolve after declining Move entirely.");
        }

        [TestMethod]
        public void PlayCouncilMember_DeclineAfterMovingOneTroop_KeepsTheFirstMoveAndStopsThere()
        {
            var scenario = MatchScenario.Build();
            var (_, target1, target2, dest1, _) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            scenario.ClickTarget(dest1, null);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed - and still legally targetable (target2 remains).");

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(PlayerColor.Blue, dest1.Occupant, "The already-resolved first move must be KEPT, not rolled back.");
            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant, "The 2nd enemy troop was never touched - declined before being targeted.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void MoveTroopCommand_DispatchedTwiceAgainstTheSameFirstMove_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (_, target1, target2, dest1, dest2) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new MoveTroopCommand(target1.Id, dest1.Id, card.Id));

            Assert.AreEqual(PlayerColor.Blue, dest1.Occupant, "The move should have applied exactly once, not twice.");
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState,
                "The repeat must not have been double-consumed - exactly 1 more should still be owed.");
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant, "target2 must remain untouched by the rejected replay.");

            // Finish the sequence normally to prove the repeat counter wasn't corrupted.
            scenario.ClickTarget(target2, null);
            scenario.ClickTarget(dest2, null);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void DeclineRepeatCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount,
                "The Promote credit banked by the first (successful) decline must not be banked again by the rejected replay.");
        }

        // --- Edge case: cancelling mid-repeat must revert BOTH the pending sequence AND the
        // already-resolved first move, not just leave the second move un-picked - distinct from
        // DeclineRepeatCommand above, which deliberately keeps progress instead. ---

        [TestMethod]
        public void CancelTargeting_MidwayThroughCouncilMemberRepeat_RevertsTheAlreadyResolvedFirstMoveToo()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2, dest1, _) = SetupRedWithTwoAdjacentEnemyTroopsAndDestinations(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "council_member");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            scenario.ClickTarget(dest1, null);
            Assert.AreEqual(PlayerColor.Blue, dest1.Occupant, "Setup check: the first move resolved.");

            scenario.Context.ActionSystem.CancelTargeting();

            Assert.AreEqual(PlayerColor.None, dest1.Occupant, "Cancelling mid-repeat must revert the whole card play, including the already-resolved first move.");
            Assert.AreEqual(PlayerColor.Blue, target1.Occupant, "target1's troop must be restored - the move must be fully undone.");
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant, "target2 was never touched.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount,
                "The Promote effect never got to run either - the whole card play was undone.");
            Assert.Contains(card.DefinitionId, red.Hand.Select(c => c.DefinitionId).ToList(), "The card itself should be restored to hand on cancel.");
        }
    }
}
