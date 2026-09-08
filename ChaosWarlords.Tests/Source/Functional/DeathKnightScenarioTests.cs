using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Death Knight ("Supplant a troop. Gain 1 VP for every 5
    /// player troops in your trophy hall.") - an unrestricted Supplant (no TargetNeutralTroopOnly,
    /// same shape as the already-shipped Wight) followed by a second, non-targeting
    /// GainResource(VictoryPoints) effect using the new DynamicAmountSource.PlayerTrophyHallCount
    /// case - the printed card explicitly says "player troops," excluding captured white/
    /// unaligned (Neutral) troops from the count, unlike Beholder's TrophyHallCount (every troop,
    /// any color). Loads the REAL "death_knight" entry out of the REAL cards.json and dispatches
    /// every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DeathKnightScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithAdjacentTroop(MatchScenario scenario, PlayerColor occupant)
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
        public void PlayDeathKnight_SupplantingAnEnemyTroop_CountsItTowardPlayerTrophyHallCount()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            red.SetTrophyHall(9, PlayerColor.Blue); // Setup only - pre-existing Blue trophies.
            var card = scenario.GiveCard(PlayerColor.Red, "death_knight");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
            Assert.IsFalse(scenario.Context.ActionSystem.CurrentSourceEffect!.TargetNeutralTroopOnly, "Death Knight's Supplant must be unrestricted - any troop, not just white ones.");

            scenario.ClickTarget(target, null);

            Assert.AreEqual(red.Color, target.Occupant, "Red's troop should have Supplanted the Blue one.");
            Assert.AreEqual(10, red.TrophyHall, "The freshly-captured Blue troop must join the 9 pre-existing ones.");
            Assert.AreEqual(vpBefore + 2, red.VictoryPoints, "10 player troops / 5 per VP = 2 VP.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        [TestMethod]
        public void PlayDeathKnight_SupplantingANeutralTroop_DoesNotCountItTowardPlayerTrophyHallCount()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            red.SetTrophyHall(9, PlayerColor.Blue); // 9 real player troops already banked.
            var card = scenario.GiveCard(PlayerColor.Red, "death_knight");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(red.Color, target.Occupant, "Red's troop should have Supplanted the Neutral one.");
            Assert.AreEqual(10, red.TrophyHall, "TrophyHall's TOTAL count includes the Neutral capture.");
            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "Only the 9 pre-existing Blue troops count - the freshly-captured Neutral one must NOT: 9 / 5 = 1 VP, not 10 / 5 = 2.");
        }

        // --- Row 3: no-valid-target fallback for the Supplant half - the VP half must still
        // fire, counting whatever player troops were already in the trophy hall. ---

        [TestMethod]
        public void PlayDeathKnight_NoTroopsAnywhereOnTheBoard_SkipsSupplantButStillGrantsVPFromExistingPlayerTrophies()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(12, PlayerColor.Orange); // Setup only - pre-existing trophies from earlier turns.
            var card = scenario.GiveCard(PlayerColor.Red, "death_knight");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid targets anywhere means Supplant should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(12, red.TrophyHall, "The trophy hall must be unchanged - Supplant never fired.");
            Assert.AreEqual(vpBefore + 2, red.VictoryPoints, "The VP half is unrelated to whether Supplant found a target and must still apply: 12 / 5 = 2.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayDeathKnightCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "death_knight");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Death Knight should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, blue.VictoryPoints);
        }

        // --- Row 5: stale/nonexistent/empty target ---

        [TestMethod]
        public void SupplantCommand_TargetingANonexistentNode_IsRejectedWhileDeathKnightEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "death_knight");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void SupplantCommand_TargetingAnEmptyNode_IsRejectedWhileDeathKnightEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var (_, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var emptyNode = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != target);

            var card = scenario.GiveCard(PlayerColor.Red, "death_knight");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(emptyNode.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An empty node (no troop at all) must be rejected.");
        }

        // --- Row 7/8: double-dispatch/replay and rapid dispatch ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheSameTarget_SecondDispatchIsRejectedAndDoesNotDoubleGrantVP()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            red.SetTrophyHall(9, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "death_knight");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(target.Id, card.Id));

            Assert.AreEqual(10, red.TrophyHall, "Should have been Supplanted exactly once, not twice.");
            Assert.AreEqual(red.Color, target.Occupant);
            Assert.AreEqual(vpBefore + 2, red.VictoryPoints, "VP should have been granted exactly once (10/5=2), not twice.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
