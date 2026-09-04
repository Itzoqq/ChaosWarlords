using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Olhydra ("Supplant a white troop anywhere on the
    /// board. Focus: Deploy 2 troops.") - Supplant(TargetNeutralTroopOnly,
    /// IgnoresPresenceRequirement) then a Focus-gated GainResource(TargetResource: Troops,
    /// Amount: 2). Loads the REAL "olhydra" entry out of the REAL cards.json and dispatches
    /// every command through a REAL CommandDispatcher. Keeps the matrix lean - doesn't re-prove
    /// every row already covered individually by OgreZombieScenarioTests (same Supplant shape)
    /// and CrushingWaveCultistScenarioTests (same Focus-gated GainResource(Troops) shape) -
    /// and focuses on the combination instead.
    ///
    /// The Deploy half here is an automatic, non-optional GainResource(TargetResource: Troops)
    /// effect - unlike Kobold/Master of Melee-Magthere, where the equivalent Deploy is offered
    /// as an optional accept/decline choice.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class OlhydraScenarioTests
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

        // --- Row 1: mundane happy path (WITH Presence, WITHOUT Focus) - proves the Supplant
        // half's neutral-only filter and the Focus-gating filter both function via real
        // dispatch. ---

        [TestMethod]
        public void PlayOlhydra_WithPresenceAndWithoutFocus_SupplantsTheNeutralTroopAndSkipsTheDeployHalf()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            int barracksBefore = red.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "olhydra"); // Only card in hand of its Aspect -> no Focus.

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant, "Red's troop should have Supplanted the Neutral one.");
            Assert.AreEqual(1, red.TrophyHall);
            // Supplant itself deploys 1 troop (Supplant = Assassinate + Deploy) - only the
            // SECOND effect (GainResource, TargetResource: Troops, Amount 2) is what Focus
            // gates, so the expected barracks delta here is -1, not 0.
            Assert.AreEqual(barracksBefore - 1, red.TroopsInBarracks, "Without Focus, the Deploy-2-troops SECOND effect must not fire at all - only Supplant's own built-in deploy (-1) should have happened.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 1: full intended behavior (no Presence needed, WITH Focus) - previously
        // blocked by the now-FIXED IgnoresPresenceRequirement JSON plumbing bug (see
        // OgreZombieScenarioTests). ---

        [TestMethod]
        public void PlayOlhydra_NeutralTroopWithNoPresenceAnywhereNearby_StillSupplantsIt()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral;

            var card = scenario.GiveCard(PlayerColor.Red, "olhydra");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "IgnoresPresenceRequirement should let a zero-Presence Neutral troop still count as a valid target.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        // --- Row 1: with Focus, both halves fire for real - Supplant's own built-in deploy
        // (-1 barracks) plus the Focus-gated GainResource(Troops, Amount 2), which credits
        // PendingFreeTroops rather than touching the barracks directly. ---

        [TestMethod]
        public void PlayOlhydra_WithPresenceAndWithFocus_SupplantsTheNeutralTroopAndCreditsTwoFreeTroops()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            int barracksBefore = red.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "olhydra");
            scenario.GiveCard(PlayerColor.Red, "advance_scout"); // Same Aspect (Warlord) in hand -> Focus.

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant, "The Supplant half must resolve.");
            Assert.AreEqual(1, red.TrophyHall);

            // Supplant's own built-in deploy (-1 barracks) still happens as before. The
            // SECOND effect (GainResource, TargetResource: Troops, Amount: 2) is Focus-gated
            // and, once switched from the never-implemented EffectType.DeployUnit, credits
            // PendingFreeTroops instead of touching the barracks directly - same mechanism as
            // Skeletal Horde/CrushingWaveCultistScenarioTests.
            Assert.AreEqual(barracksBefore - 1, red.TroopsInBarracks, "Only Supplant's own built-in deploy (-1) should touch the barracks - the Focus-gated GainResource half credits PendingFreeTroops instead.");
            Assert.AreEqual(2, red.PendingFreeTroops, "With Focus active, the second effect should credit 2 free troops.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayOlhydraCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "olhydra");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "olhydra");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
