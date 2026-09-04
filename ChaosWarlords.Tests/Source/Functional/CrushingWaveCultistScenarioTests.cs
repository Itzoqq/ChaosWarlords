using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Crushing Wave Cultist ("Assassinate a white troop.
    /// Focus: Deploy 2 troops.") - Assassinate(TargetNeutralTroopOnly) then a Focus-gated
    /// GainResource(TargetResource: Troops, Amount: 2). Loads the REAL "crushing_wave_cultist"
    /// entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    ///
    /// The Deploy half is EffectType.GainResource with TargetResource: Troops, the same
    /// mechanism every other shipped "Deploy N troops" card (e.g. Skeletal Horde) uses: it
    /// credits Player.PendingFreeTroops immediately (an automatic, non-optional effect -
    /// ProcessAutomaticEffect applies it and pops the stack in one step, no dedicated
    /// IEffectStrategy needed), and those pending troops are later spent one at a time via the
    /// normal DeployTroopCommand flow (CombatResolver.ExecuteDeploy/ExecuteAssassinate both
    /// check PendingFreeTroops first).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CrushingWaveCultistScenarioTests
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

        // --- Row 1: positive/happy path, WITHOUT Focus - the Deploy half is filtered out
        // entirely by CardEffectProcessor.ResolveEffects before it's ever pushed onto the
        // stack, so this test never touches the GainResource(Troops) path at all. ---

        [TestMethod]
        public void PlayCrushingWaveCultist_WithoutFocus_AssassinatesTheNeutralTroopAndSkipsTheDeployHalf()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            int barracksBefore = red.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "crushing_wave_cultist"); // Only card in hand of its Aspect -> no Focus.

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant, "The Neutral troop should have been assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(barracksBefore, red.TroopsInBarracks, "Without Focus, the Deploy-2-troops half must not fire at all.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 1 (with Focus): the headline behavior - Focus turns on the second effect,
        // which now actually credits free troops via GainResource(Troops). ---

        [TestMethod]
        public void PlayCrushingWaveCultist_WithFocus_AssassinatesTheNeutralTroopAndCreditsTwoFreeTroops()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            int barracksBefore = red.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "crushing_wave_cultist");
            // A second card of the SAME Aspect (Warlord) in hand grants Focus - see
            // MatchManager.PlayCard's hasFocus computation ("reveal from hand").
            scenario.GiveCard(PlayerColor.Red, "advance_scout");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant, "The Assassinate half must still resolve.");
            Assert.AreEqual(1, red.TrophyHall);

            // With Focus active the second effect (GainResource, TargetResource: Troops,
            // Amount: 2) fires as an automatic, non-optional effect - it credits
            // PendingFreeTroops immediately rather than touching the barracks directly (same
            // mechanism as Skeletal Horde's "Deploy 2 troops").
            Assert.AreEqual(barracksBefore, red.TroopsInBarracks, "GainResource(Troops) credits PendingFreeTroops - it must not touch the barracks directly.");
            Assert.AreEqual(2, red.PendingFreeTroops, "With Focus active, the second effect should credit 2 free troops.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
            // PendingFreeTroops actually being spendable through the real DeployTroopCommand
            // path (CombatResolver.ExecuteDeploy's PendingFreeTroops priority) is covered by
            // MapManagerTests/CombatResolver-level tests, not re-proven card-by-card here -
            // same convention SkeletalHordeScenarioTests/CraniumRatsScenarioTests already use.
        }

        // --- Row 3: no-valid-target fallback for the Assassinate half ---

        [TestMethod]
        public void PlayCrushingWaveCultist_OnlyEnemyTroopsReachable_SkipsAssassinate()
        {
            var scenario = MatchScenario.Build();
            var (red, enemyTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "crushing_wave_cultist");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant);
            Assert.AreEqual(0, red.TrophyHall);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayCrushingWaveCultistCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "crushing_wave_cultist");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        // --- Row 5: illegal Assassinate target rejected server-side ---

        [TestMethod]
        public void AssassinateCommand_TargetingAnActualPlayersTroop_IsRejectedWhileCrushingWaveCultistEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "crushing_wave_cultist");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand);

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "crushing_wave_cultist");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new AssassinateCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
