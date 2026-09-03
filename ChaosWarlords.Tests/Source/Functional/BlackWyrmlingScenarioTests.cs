using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Black Wyrmling ("+1 Power. Assassinate a white
    /// troop.") - identical shape to Ravenous Zombies (unconditional GainResource, then a
    /// TargetNeutralTroopOnly Assassinate), so this mirrors RavenousZombiesScenarioTests.cs
    /// directly, just against the REAL "black_wyrmling" cards.json entry. No
    /// IgnoresPresenceRequirement, no RequiresFocus, no IsOptional/Alternative on this card.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BlackWyrmlingScenarioTests
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
        public void PlayBlackWyrmling_WithNeutralTroopPresent_GainsPowerAndAssassinatesTheNeutralTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "black_wyrmling");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Power, "The +1 Power half should resolve immediately, before the Assassinate click.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            Assert.IsNotNull(scenario.Context.ActionSystem.CurrentSourceEffect);
            Assert.IsTrue(scenario.Context.ActionSystem.CurrentSourceEffect!.TargetNeutralTroopOnly, "Black Wyrmling's Assassinate effect must carry the neutral-only restriction.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant, "The Neutral troop should have been assassinated (node cleared).");
            Assert.AreEqual(1, red.TrophyHall, "Assassinate should have awarded a trophy.");
            Assert.AreEqual(1, red.Power, "Power should not have changed again during the Assassinate half.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayBlackWyrmling_OnlyEnemyTroopsReachable_SkipsAssassinateButStillGrantsPower()
        {
            var scenario = MatchScenario.Build();
            var (red, enemyTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "black_wyrmling");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Power, "The +1 Power half must still resolve even though Assassinate has no valid target.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Neutral target means the card should fully resolve, not sit blocked on impossible targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant, "The enemy troop must survive untouched - it was never a legal target for this card.");
            Assert.AreEqual(0, red.TrophyHall);
        }

        [TestMethod]
        public void PlayBlackWyrmling_NoTroopsAnywhereOnTheBoard_SkipsAssassinateButStillGrantsPower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "black_wyrmling");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Power);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayBlackWyrmlingCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "black_wyrmling");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Black Wyrmling should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: illegal filtered target rejected server-side ---

        [TestMethod]
        public void AssassinateCommand_TargetingAnActualPlayersTroop_IsRejectedWhileBlackWyrmlingEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color); // Setup only - grants Presence at every node of this site.

            var card = scenario.GiveCard(PlayerColor.Red, "black_wyrmling");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "A real Neutral target exists, so targeting must have started.");

            var forgedCommand = new AssassinateCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected while TargetNeutralTroopOnly is in effect.");

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant, "Blue's troop must survive - it was never a legal target for this card's Assassinate.");
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileBlackWyrmlingEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "black_wyrmling");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "black_wyrmling");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new AssassinateCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The Neutral troop should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
