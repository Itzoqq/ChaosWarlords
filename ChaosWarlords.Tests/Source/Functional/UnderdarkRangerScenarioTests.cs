using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Underdark Ranger ("Assassinate 2 white troops.") - the
    /// first shipped card combining Deathblade's exact-N repeat shape (IEffectStrategy.
    /// SupportsRepeat + CardEffect.Amount) with Ravenous Zombies' CardEffect.
    /// TargetNeutralTroopOnly restriction, both already-established primitives - pure data, no
    /// new engine code. Loads the REAL "underdark_ranger" entry out of the REAL cards.json and
    /// dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class UnderdarkRangerScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with at least 2 empty neighbors, then marks 2 of those
        /// neighbors as Neutral troops - Red has Presence at both via the deployed troop's
        /// adjacency, matching DeathbladeScenarioTests.cs's own setup helper.
        /// </summary>
        private static (Player red, MapNode target1, MapNode target2) SetupRedWithTwoAdjacentNeutralTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Count(neighbor => neighbor.Occupant == PlayerColor.None) >= 2);
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));

            var emptyNeighbors = redNode.Neighbors.Where(n => n.Occupant == PlayerColor.None).Take(2).ToList();
            emptyNeighbors[0].Occupant = PlayerColor.Neutral; // Setup only - not going through a command.
            emptyNeighbors[1].Occupant = PlayerColor.Neutral;

            return (red, emptyNeighbors[0], emptyNeighbors[1]);
        }

        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayUnderdarkRanger_WithTwoNeutralTroopsReachable_AssassinatesBothWithoutSpendingPower()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");
            int powerBefore = red.Power;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            Assert.IsTrue(scenario.Context.ActionSystem.CurrentSourceEffect!.TargetNeutralTroopOnly, "Underdark Ranger's Assassinate effect must carry the neutral-only restriction.");

            scenario.ClickTarget(target1, null);
            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "One more target is still owed.");

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(PlayerColor.None, target2.Occupant);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(powerBefore, red.Power, "Both assassinations are card-funded - neither should spend Power.");
        }

        // --- Row 3: no-valid-target fallback, plus the "fewer than requested targets exist" edge case ---

        [TestMethod]
        public void PlayUnderdarkRanger_ExactlyOneNeutralTroopReachable_AssassinatesItAndResolvesWithoutASecondClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var onlyTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            onlyTarget.Occupant = PlayerColor.Neutral; // The ONLY neutral troop anywhere on the board.

            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(onlyTarget, null);

            Assert.AreEqual(PlayerColor.None, onlyTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "Only 1 neutral troop existed - the effect must not demand an impossible 2nd.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayUnderdarkRanger_OnlyEnemyTroopsReachable_SkipsAssassinateEntirely()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var enemyTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            enemyTarget.Occupant = PlayerColor.Blue; // Not Neutral - never a legal target for this card.

            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Neutral target means the effect should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant, "The enemy troop must survive untouched.");
            Assert.AreEqual(0, red.TrophyHall);
        }

        [TestMethod]
        public void PlayUnderdarkRanger_NoTroopsAnywhereOnTheBoard_SkipsAssassinateEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.IsEmpty(scenario.Interactions, "No optional-effect popup exists on this card.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayUnderdarkRangerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "underdark_ranger");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Underdark Ranger should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/illegal target for the SECOND repeat specifically ---

        [TestMethod]
        public void AssassinateCommand_TargetingAnActualPlayersTroop_IsRejectedForTheSecondUnderdarkRangerTarget()
        {
            // A node occupied by an actual PLAYER's troop is a perfectly legal target for an
            // ORDINARY, unfiltered Assassinate - Validate() must independently re-derive and
            // enforce the neutral-only restriction from ActionSystem.CurrentSourceEffect for
            // EVERY repeat, not just the first.
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentNeutralTroops(scenario);

            // A separate site, given Presence via a spy (RavenousZombiesScenarioTests.cs's own
            // pattern) rather than another neighbor of redNode - decouples this from exactly how
            // many empty neighbors the shared test map's chosen redNode happens to have.
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s.NodesInternal[0].Occupant == PlayerColor.None);
            var blueTroop = site.NodesInternal[0];
            blueTroop.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");
            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(1, red.TrophyHall, "Setup check: first repeat resolved.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Still 1 more target owed.");

            var forgedCommand = new AssassinateCommand(blueTroop.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected even for the 2nd repeat.");

            Assert.AreEqual(PlayerColor.Blue, blueTroop.Occupant, "Blue's troop must survive.");
            Assert.AreEqual(1, red.TrophyHall, "The rejected re-target must not grant a second trophy.");
        }

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForTheSecondUnderdarkRangerTarget()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(1, red.TrophyHall);

            // target1 is now an empty node (already assassinated) - re-targeting it for the
            // SECOND repeat must be rejected exactly like any other empty node would be.
            var forgedCommand = new AssassinateCommand(target1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-assassinated (now empty) node must be rejected as a target for the 2nd repeat.");
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileUnderdarkRangerEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7/8: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new AssassinateCommand(target1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "target1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed - exactly 1 more target should still be owed.");
            Assert.AreEqual(PlayerColor.Neutral, target2.Occupant, "target2 must remain untouched by the rejected replay.");

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheNeutralTroopThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "underdark_ranger");
            scenario.PlayCard(card);

            var command = new AssassinateCommand(target1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into an AssassinateCommand.");
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }
    }
}
