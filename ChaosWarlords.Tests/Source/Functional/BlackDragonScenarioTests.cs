using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Black Dragon ("Supplant a white troop anywhere on the
    /// board. Gain 1 VP for every 3 white troops in your trophy hall.") - Ogre Zombie's exact
    /// Supplant(TargetNeutralTroopOnly, IgnoresPresenceRequirement) shape, followed by a second,
    /// non-targeting GainResource(VictoryPoints) effect using the new
    /// DynamicAmountSource.NeutralTrophyHallCount case - the mirror image of Death Knight's
    /// PlayerTrophyHallCount (counts ONLY captured white/unaligned troops, not any actual
    /// player's). Both top-level effects resolve in array order, so the VP half counts the
    /// trophy hall AFTER this card's own Supplant has (or hasn't) added a fresh Neutral troop to
    /// it. Loads the REAL "black_dragon" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BlackDragonScenarioTests
    {
        // --- Row 1: positive/happy path, exercising IgnoresPresenceRequirement + the dynamic amount ---

        [TestMethod]
        public void PlayBlackDragon_WithEightNeutralTrophiesAlready_SupplantsWithNoPresenceAndGrantsVPCountingTheFreshNinth()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(8, PlayerColor.Neutral); // Setup only - pre-existing trophies from earlier turns.
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral; // Far from any Red presence - proves IgnoresPresenceRequirement.
            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "IgnoresPresenceRequirement should let a zero-Presence Neutral troop still count as a valid target.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant, "Red's troop should have Supplanted the Neutral one despite having no Presence there.");
            Assert.AreEqual(9, red.TrophyHall, "The freshly-Supplanted Neutral troop must be counted alongside the 8 pre-existing ones.");
            Assert.AreEqual(vpBefore + 3, red.VictoryPoints, "9 Neutral troops / 3 per VP = 3 VP.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        [TestMethod]
        public void PlayBlackDragon_WithNoPriorTrophies_SupplantsButFloorsVPToZero()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral;
            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(1, red.TrophyHall, "Only the just-Supplanted troop is in the trophy hall.");
            Assert.AreEqual(vpBefore, red.VictoryPoints, "1 Neutral troop / 3 per VP must floor to 0, not round up.");
        }

        [TestMethod]
        public void PlayBlackDragon_PlayerTrophiesDoNotCountTowardTheDynamicAmount()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(new System.Collections.Generic.Dictionary<PlayerColor, int> { [PlayerColor.Neutral] = 2, [PlayerColor.Blue] = 10 });
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral;
            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "Only the 3 Neutral troops (2 + the fresh one) should count: 3 / 3 = 1 VP, ignoring the 10 Blue trophies entirely.");
        }

        // --- Row 3: no-valid-target fallback for the Supplant half - the VP half must still
        // fire, counting whatever Neutral trophies were already banked. ---

        [TestMethod]
        public void PlayBlackDragon_NoNeutralTroopAnywhereOnTheBoard_SkipsSupplantButStillGrantsVPFromExistingNeutralTrophies()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(9, PlayerColor.Neutral); // Setup only - pre-existing trophies from earlier turns.
            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Neutral target anywhere means Supplant should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(9, red.TrophyHall, "The trophy hall must be unchanged - Supplant never fired.");
            Assert.AreEqual(vpBefore + 3, red.VictoryPoints, "The VP half is unrelated to whether Supplant found a target and must still apply: 9 / 3 = 3.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayBlackDragonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "black_dragon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Black Dragon should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, blue.VictoryPoints);
        }

        // --- Row 5: illegal/stale targets rejected server-side ---

        [TestMethod]
        public void SupplantCommand_TargetingAnActualPlayersTroop_IsRejectedWhileBlackDragonEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;

            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected while TargetNeutralTroopOnly is in effect - IgnoresPresenceRequirement never overrides the neutral-only filter.");

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant);
        }

        [TestMethod]
        public void SupplantCommand_TargetingANonexistentNode_IsRejectedWhileBlackDragonEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral;
            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheSameTarget_SecondDispatchIsRejectedAndDoesNotDoubleGrantVP()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(8, PlayerColor.Neutral);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral;
            var card = scenario.GiveCard(PlayerColor.Red, "black_dragon");
            int vpBefore = red.VictoryPoints;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(9, red.TrophyHall, "Should have been Supplanted exactly once, not twice.");
            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(vpBefore + 3, red.VictoryPoints, "VP should have been granted exactly once (9/3=3), not twice.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
