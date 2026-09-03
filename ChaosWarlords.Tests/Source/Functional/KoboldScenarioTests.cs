using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Kobold ("Choose one: Deploy a troop. Or, Assassinate
    /// a white troop.") - a true "choose one" via GainResource(TargetResource: Troops,
    /// IsOptional: true, Alternative: Assassinate(TargetNeutralTroopOnly: true)), same
    /// IsOptional+Alternative decline shape as Wight/Cloaker. Loads the REAL "kobold" entry
    /// out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    ///
    /// The Deploy half used to be EffectType.DeployUnit, which had zero production
    /// implementation anywhere - fixed for the AUTOMATIC/non-optional case (see
    /// CrushingWaveCultistScenarioTests) by switching to GainResource(TargetResource: Troops).
    /// That data fix alone did NOT fully fix Kobold, though - a SEPARATE production bug existed
    /// for the OPTIONAL accept path specifically: ActionExecutionEngine.HandleOptionalEffectAccepted
    /// used to only call ResolveCurrentEffect for EffectType.Devour, so every other optional
    /// effect type (GainResource included) fell through to a "continue to normal targeting
    /// flow" branch and was expected to resolve via a subsequent targeting click instead.
    /// GainResource is a non-targeting effect (DefaultStrategy.GetTargetingState returns
    /// ActionState.Normal), so no such click was ever coming - the pushed EffectContext was
    /// never resolved and never popped, a genuine ExecutionStack hang (CurrentState reporting
    /// Normal masked the corruption). FIXED: HandleOptionalEffectAccepted now has a generic
    /// fallback that applies the effect and resolves the stack immediately for any accepted
    /// optional non-targeting effect, not just Devour. The "decline" direction was always
    /// unaffected (it correctly calls ResolveCurrentEffect(false) and reaches the Assassinate
    /// Alternative normally) - as was the FindFirstEffect pre-check bug for the decline path
    /// (row 3 below), which the AssassinateStrategy.FindFirstEffect.Alternative-recursion fix
    /// DID resolve.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class KoboldScenarioTests
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

        // --- Row 2 (decline direction): reaches the Assassinate Alternative correctly ---

        [TestMethod]
        public void PlayKobold_DeclineTheOptionalDeploy_AssassinatesTheNeutralTroopAlternativeInstead()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "kobold");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Playing Kobold should raise exactly one optional-effect popup.");

            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Declining the Deploy should chain into the Assassinate Alternative.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant, "The Neutral troop should have been assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 2 (accept direction): the optional Deploy branch now applies+resolves
        // immediately - see class doc comment history. Kobold's Deploy is OPTIONAL and
        // non-targeting (GainResource), and ActionExecutionEngine.HandleOptionalEffectAccepted
        // now has a generic fallback that applies the effect and calls ResolveCurrentEffect for
        // any accepted optional effect that isn't a targeting effect, not just Devour. ---

        [TestMethod]
        public void PlayKobold_AcceptTheOptionalDeploy_CreditsPendingFreeTroopsAndResolvesTheStack()
        {
            // FIXED: accepting Kobold's optional GainResource(Troops) now credits
            // PendingFreeTroops and resolves the pushed EffectContext -
            // ActionExecutionEngine.HandleOptionalEffectAccepted applies the effect and calls
            // ResolveCurrentEffect(true) for any accepted optional non-targeting effect (not
            // just Devour), so the stack no longer strands an unresolved effect.
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            int barracksBefore = red.TroopsInBarracks;
            int pendingBefore = red.PendingFreeTroops;
            var card = scenario.GiveCard(PlayerColor.Red, "kobold");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);

            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(barracksBefore, red.TroopsInBarracks, "Deploy via GainResource never touches the barracks directly - it credits PendingFreeTroops instead.");
            Assert.AreEqual(pendingBefore + 1, red.PendingFreeTroops, "Accepting Kobold's Deploy should credit exactly 1 pending free troop (its GainResource Amount).");
            Assert.AreEqual(0, red.TrophyHall, "Choose-one mutual exclusivity holds: the Assassinate Alternative must NOT also fire.");
            Assert.AreEqual(PlayerColor.Neutral, neutralTarget.Occupant, "The Alternative's target must be untouched - it was never reached.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The action system must fully settle back to Normal after the optional effect resolves.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "The accepted effect must be fully resolved and popped, not stranded, so it can't ambush the next card played.");
        }

        // --- Row 3: no-valid-target fallback for the decline->Assassinate path ---

        [TestMethod]
        public void PlayKobold_DeclineWithOnlyEnemyTroopsReachable_SkipsTheAlternativeEntirely()
        {
            // FIXED (see RESOLVED.txt/planning.txt): the "should this Alternative even be
            // offered" pre-check (AssassinateStrategy.HasValidTargets -> FindFirstEffect) used
            // to only recurse into CardEffect.OnSuccess, never CardEffect.Alternative - so for
            // Kobold (whose neutral-only Assassinate lives UNDER GainResource.Alternative, not
            // .OnSuccess), FindFirstEffect returned null and the neutral-only restriction was
            // silently lost for this pre-check specifically, incorrectly entering
            // TargetingAssassinate even with no legal Neutral target reachable.
            // FindFirstEffect now recurses into .Alternative too, so with only a non-Neutral
            // troop reachable, declining correctly skips the Assassinate Alternative entirely
            // and returns straight to ActionState.Normal.
            var scenario = MatchScenario.Build();
            var (red, enemyTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "kobold");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);

            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "With only a non-Neutral troop reachable, declining the Deploy should skip the Assassinate Alternative entirely (no legal target) rather than incorrectly entering TargetingAssassinate.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant, "The enemy troop must be untouched - it was never a legal target.");
            Assert.AreEqual(0, red.TrophyHall);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayKoboldCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "kobold");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.IsEmpty(scenario.Interactions, "Nothing should have been resolved at all for a rejected play.");
        }

        // --- Row 5: illegal Assassinate target rejected server-side (decline path) ---

        [TestMethod]
        public void AssassinateCommand_TargetingAnActualPlayersTroop_IsRejectedWhileKoboldAlternativeIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "kobold");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand);

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant);
        }

        // --- Row 7: double-dispatch/replay (decline path) ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "kobold");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new AssassinateCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 7b: replaying PlayCardCommand itself must not raise a second popup ---

        [TestMethod]
        public void PlayKoboldCommand_DispatchedTwice_SecondDispatchRaisesNoAdditionalPopup()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "kobold");

            var playCommand = new PlayCardCommand(card);
            scenario.Dispatch(playCommand);
            Assert.HasCount(1, scenario.Interactions);

            scenario.Dispatch(playCommand); // Replay of the exact same command instance.

            Assert.HasCount(1, scenario.Interactions, "Re-dispatching an already-resolved PlayCardCommand must not raise a second optional-effect popup.");
        }
    }
}
