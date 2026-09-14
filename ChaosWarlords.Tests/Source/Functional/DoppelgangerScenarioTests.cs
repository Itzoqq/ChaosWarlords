using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Doppelganger ("Supplant a troop.") - planning.txt TIER 1
    /// item 8. A single mandatory, unrestricted Supplant (any troop, not TargetNeutralTroopOnly),
    /// the same shape test_infiltrator already exercises among TrivialPrimitiveCardsScenarioTests
    /// - this file mirrors that setup for the real shipped card. Loads the REAL "doppelganger"
    /// entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DoppelgangerScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithAdjacentBlueTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = blue.Color;

            return (red, target);
        }

        // --- Row 1: positive/happy path ---

        [TestMethod]
        public void PlayDoppelganger_SupplantsTheChosenTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "doppelganger");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(red.Color, target.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayDoppelganger_NoEnemyTroopsAnywhere_SkipsEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "doppelganger");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Supplant target anywhere - must resolve cleanly instead of stalling.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayDoppelgangerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "doppelganger");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void SupplantCommand_TargetingANonexistentNode_IsRejectedWhileDoppelgangerEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "doppelganger");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new SupplantCommand(999999, card.Id), "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 6: unmet resource precondition - Doppelganger's Supplant deploy half is always
        // free regardless of barracks state (tyrants-rules skill section 5's empty-barracks
        // clause), so there is no resource precondition for this card to fail. ---

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayDoppelgangerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "doppelganger");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
        }
    }
}
