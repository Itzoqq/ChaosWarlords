using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Master of Melee-Magthere ("Choose one: Deploy 4
    /// troops. Or, Supplant a white troop anywhere on the board.") - a choose-one via
    /// GainResource(TargetResource: Troops, Amount: 4, IsOptional: true, Alternative:
    /// Supplant(TargetNeutralTroopOnly, IgnoresPresenceRequirement)). Loads the REAL
    /// "master_of_melee_magthere" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher.
    ///
    /// Exercises three things together that no other single card's test matrix covers at once:
    /// IgnoresPresenceRequirement flowing all the way from JSON through
    /// CardFactory.ParseOptionalFlags to the runtime CardEffect (same as Ogre Zombie);
    /// SupplantStrategy.FindFirstEffect recursing into CardEffect.Alternative (this card's
    /// Supplant lives under GainResource.Alternative, not .OnSuccess); and accepting the
    /// optional "Deploy 4 troops" branch resolving immediately via
    /// ActionExecutionEngine.HandleOptionalEffectAccepted's generic non-targeting-effect
    /// fallback, rather than stranding an unresolved effect on ActionSystem.ExecutionStack.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MasterOfMeleeMagthereScenarioTests
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

        // --- Row 2 (decline direction), mundane case: proves decline->Supplant plumbing works
        // for THIS card via real JSON when Presence is available anyway (not exercising the
        // presence-override itself - see the dedicated no-Presence test below). ---

        [TestMethod]
        public void PlayMasterOfMeleeMagthere_DeclineTheOptionalDeploy_SupplantsANeutralTroopWherePresenceIsAvailable()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "master_of_melee_magthere");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Playing this card should raise exactly one optional-effect popup.");

            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Declining the Deploy should chain into the Supplant Alternative.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant, "Red's troop should have Supplanted the Neutral one.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 2 (decline direction): proves IgnoresPresenceRequirement actually lets a
        // zero-Presence Neutral troop count as a valid Supplant target for this card's
        // Alternative (see class doc comment). ---

        [TestMethod]
        public void PlayMasterOfMeleeMagthere_DeclineTheOptionalDeploy_SupplantsANeutralTroopWithNoPresenceAnywhereNearby()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var neutralTarget = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            neutralTarget.Occupant = PlayerColor.Neutral; // Setup only - far from any Red presence.

            var card = scenario.GiveCard(PlayerColor.Red, "master_of_melee_magthere");
            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);

            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "IgnoresPresenceRequirement should let a zero-Presence Neutral troop still count as a valid target for the Alternative.");

            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        // --- Row 2 (accept direction): the optional Deploy branch applies+resolves
        // immediately - see class doc comment. ---

        [TestMethod]
        public void PlayMasterOfMeleeMagthere_AcceptTheOptionalDeploy_CreditsPendingFreeTroopsAndResolvesTheStack()
        {
            // Accepting the optional GainResource(Troops) credits PendingFreeTroops and resolves
            // the pushed EffectContext - ActionExecutionEngine.HandleOptionalEffectAccepted
            // applies the effect and calls ResolveCurrentEffect(true) for any accepted optional
            // non-targeting effect, not just Devour, so the stack doesn't strand an unresolved
            // effect.
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            int barracksBefore = red.TroopsInBarracks;
            int pendingBefore = red.PendingFreeTroops;
            var card = scenario.GiveCard(PlayerColor.Red, "master_of_melee_magthere");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);

            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(barracksBefore, red.TroopsInBarracks, "Deploy via GainResource never touches the barracks directly - it credits PendingFreeTroops instead.");
            Assert.AreEqual(pendingBefore + 4, red.PendingFreeTroops, "Accepting the Deploy should credit exactly 4 pending free troops (its GainResource Amount).");
            Assert.AreEqual(0, red.TrophyHall, "Choose-one mutual exclusivity holds: the Supplant Alternative must NOT also fire.");
            Assert.AreEqual(PlayerColor.Neutral, neutralTarget.Occupant, "The Alternative's target must be untouched - it was never reached.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The action system must fully settle back to Normal after the optional effect resolves.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "The accepted effect must be fully resolved and popped, not stranded, so it can't ambush the next card played.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayMasterOfMeleeMagthereCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "master_of_melee_magthere");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.IsEmpty(scenario.Interactions);
        }

        // --- Row 5: illegal Supplant target rejected server-side (decline path, mundane presence case) ---

        [TestMethod]
        public void SupplantCommand_TargetingAnActualPlayersTroop_IsRejectedWhileMasterOfMeleeMagthereAlternativeIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "master_of_melee_magthere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand);

            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant);
        }

        // --- Row 7: double-dispatch/replay (decline path) ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheNeutralTroop_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "master_of_melee_magthere");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(neutralTarget.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(red.Color, neutralTarget.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
