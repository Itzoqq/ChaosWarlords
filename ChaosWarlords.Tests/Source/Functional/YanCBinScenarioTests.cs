using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Yan-C-Bin ("Place a spy, then assassinate a troop at
    /// that spy's site. Focus: place another spy.") - confirmed in planning.txt to already be
    /// fully data-driven: the first effect is exactly Banshee's mandatory
    /// PlaceSpy-&gt;OnSuccess shape (site-scoping via ActionSystem.PendingSite, set by
    /// PlaceSpyCommand.Execute BEFORE CompleteAction() resolves the OnSuccess child - the same
    /// mechanism Cloaker/Graz'zt/Spellspinner's ReturnOwnSpy-&gt;OnSuccess chains use, just fed
    /// by a placed spy instead of a returned one), and the Focus bonus is a second, fully
    /// independent top-level effect using the existing CardEffect.RequiresFocus flag (same shape
    /// as Olhydra/Crushing Wave Cultist's Focus-gated second effect). Loads the REAL "yan_c_bin"
    /// entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class YanCBinScenarioTests
    {
        // --- Row 1: happy path, without Focus - only the mandatory PlaceSpy->Assassinate
        // chain fires; the Focus-gated second PlaceSpy must not. ---

        [TestMethod]
        public void PlayYanCBin_WithoutFocus_PlacesSpyThenAssassinatesAtThatSiteOnly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            int spiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin"); // Only Shadow card in hand -> no Focus.
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, siteA);

            Assert.Contains(red.Color, siteA.Spies, "The spy placed by the mandatory first effect must remain (never returned).");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Should chain straight into Assassinate, scoped to the just-spied site.");

            scenario.ClickTarget(troopA, null);

            Assert.AreEqual(PlayerColor.None, troopA.Occupant, "Blue's troop should have been assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(spiesBefore - 1, red.SpiesInBarracks, "Only 1 spy placed - the Focus-gated second PlaceSpy must not have fired.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 1: happy path, WITH Focus - both the chain AND the second independent
        // PlaceSpy fire. ---

        [TestMethod]
        public void PlayYanCBin_WithFocus_AlsoPlacesASecondSpyAtADifferentSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            int spiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");
            scenario.GiveCard(PlayerColor.Red, "infiltrator"); // Same Aspect (Shadow) in hand -> Focus.
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, siteA);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(troopA, null);

            Assert.AreEqual(PlayerColor.None, troopA.Occupant);
            Assert.AreEqual(1, red.TrophyHall);

            // The first (mandatory) effect is fully resolved - the second, independent,
            // Focus-gated PlaceSpy effect should now be active.
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Focus should chain into the SECOND top-level PlaceSpy effect.");
            scenario.ClickTarget(null, siteB);

            Assert.Contains(red.Color, siteA.Spies, "The first spy must still be at siteA.");
            Assert.Contains(red.Color, siteB.Spies, "The Focus-granted second spy should be at siteB.");
            Assert.AreEqual(spiesBefore - 2, red.SpiesInBarracks, "Both spies should have been placed.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3: no-valid-target fallback for both the mandatory chain and the Focus
        // bonus. ---

        [TestMethod]
        public void PlayYanCBin_NoSpiesInBarracks_DoesNothingAtAllCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0; // Neither PlaceSpy effect has a valid target.
            scenario.GiveCard(PlayerColor.Red, "infiltrator"); // Focus condition met - doesn't matter, still no spies to place.
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayYanCBin_NoSiteWithoutARedSpyAlready_SkipsCleanlyWithoutStalling()
        {
            // Red already has a spy at EVERY site - PlaceSpy's own "not already spied by me"
            // rule means the mandatory first effect has no valid target and must resolve
            // cleanly to Normal instead of stranding the player in TargetingPlaceSpy.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            foreach (var site in scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0))
            {
                site.AddSpy(red.Color);
            }
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3 (continued): the interaction between the two independent top-level
        // effects - a chained OnSuccess with no valid target while a sibling top-level effect
        // is still pending is a shape this project's chain-scoping bugs have repeatedly come
        // from, so it gets its own dedicated executable coverage rather than resting on a code
        // trace. ---

        [TestMethod]
        public void PlayYanCBin_WithFocusButNoTroopAtTheSpiedSite_StillOffersTheFocusSpyAfterwards()
        {
            // The mandatory chain's PlaceSpy half succeeds, but its OnSuccess Assassinate half
            // has no valid target at that site (no troop there at all) - TryResolveActor finds
            // no Alternative, so nothing gets pushed for it and the stack falls through to the
            // second, independent, Focus-gated PlaceSpy effect. Must resolve cleanly to that
            // effect's own TargetingPlaceSpy, not strand the player or skip it.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            // Deliberately no troop anywhere at siteA - the Assassinate half has nothing to hit.
            int spiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");
            scenario.GiveCard(PlayerColor.Red, "infiltrator"); // Same Aspect (Shadow) in hand -> Focus.
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, siteA);

            Assert.Contains(red.Color, siteA.Spies, "The first spy should still have been placed.");
            // No Assassinate target at siteA - the chain's OnSuccess must fizzle cleanly and
            // fall through to the second top-level effect instead of stalling in
            // TargetingAssassinate or silently dropping the Focus bonus.
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Should fall through directly to the Focus-gated second PlaceSpy effect.");

            scenario.ClickTarget(null, siteB);

            Assert.Contains(red.Color, siteB.Spies, "The Focus-granted second spy should be at siteB.");
            Assert.AreEqual(spiesBefore - 2, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayYanCBin_WithFocusButOnlyOneUnspiedSiteLeft_TheFirstEffectConsumesItAndTheFocusBonusSkipsCleanly()
        {
            // Only one site left without a Red spy - the mandatory first effect's PlaceSpy
            // consumes it, so the Focus-gated second PlaceSpy has zero remaining valid targets
            // and must resolve cleanly to Normal instead of stalling in TargetingPlaceSpy
            // waiting for an impossible click.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0).ToList();
            var lastUnspiedSite = sites[0];
            foreach (var site in sites.Skip(1))
            {
                site.AddSpy(red.Color);
            }

            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");
            scenario.GiveCard(PlayerColor.Red, "infiltrator"); // Same Aspect (Shadow) in hand -> Focus.
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, lastUnspiedSite);

            Assert.Contains(red.Color, lastUnspiedSite.Spies);
            // No troop was ever placed at lastUnspiedSite, so the Assassinate half already
            // fizzles here too - and now every site has a Red spy, so the Focus PlaceSpy has
            // nothing left to target either. See ActionExecutionEngine.
            // TryEnterTargetingForRequiredEffect's doc comment for the mechanism that keeps
            // this a clean skip instead of a stall.
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The Focus bonus must skip cleanly - no site left without a Red spy.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayYanCBinCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "yan_c_bin");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Yan-C-Bin should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, and Assassinate's site-scoping defense-in-depth ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheWrongSite_IsRejectedByValidateDefenseInDepth()
        {
            // Bypasses the input-layer PendingSite guard entirely - proves AssassinateCommand.
            // Validate()'s own IsAtRequiredSite check is the real defense against a forged
            // command, not just ActionInputController.HandleAssassinate's UI-layer guard.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            var troopB = siteB.NodesInternal[0];
            troopB.Occupant = blue.Color;
            var presenceNodeB = troopB.Neighbors.First(n => n.Occupant == PlayerColor.None);
            presenceNodeB.Occupant = red.Color; // Genuine Presence at siteB too - proves the wrong-site rejection is about scoping, not Presence.

            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");
            scenario.PlayCard(card);
            scenario.ClickTarget(null, siteA); // Places the spy at Site A - PendingSite now Site A.
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(troopB.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "Assassinating at Site B's troop while PendingSite is Site A must be rejected.");
            Assert.AreEqual(blue.Color, troopB.Occupant);

            scenario.ClickTarget(troopA, null); // The correct site still works.
            Assert.AreEqual(PlayerColor.None, troopA.Occupant);
        }

        [TestMethod]
        public void PlaceSpyCommand_ForASiteAlreadySpiedByRed_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            siteA.AddSpy(red.Color); // Already spied - not a valid PlaceSpy target.
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");

            scenario.PlayCard(card);

            var forgedCommand = new PlaceSpyCommand(siteA.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A site Red already has a spy at must be rejected as a PlaceSpy target.");
        }

        // --- Row 7: double-dispatch/replay, for every command type this card can produce ---

        [TestMethod]
        public void PlayYanCBinCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "The card's effect should have started exactly once.");
        }

        [TestMethod]
        public void PlaceSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            siteA.NodesInternal[0].Occupant = blue.Color; // A troop to Assassinate, so the chain has somewhere to land.
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new PlaceSpyCommand(siteA.Id, card.Id));

            Assert.HasCount(1, siteA.Spies.Where(c => c == red.Color), "The spy must have been placed exactly once.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The replay must not have advanced past the pending Assassinate.");
        }

        [TestMethod]
        public void AssassinateCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");

            scenario.PlayCard(card);
            scenario.ClickTarget(null, siteA);
            scenario.DispatchTwice(new AssassinateCommand(troopA.Id, card.Id));

            Assert.AreEqual(PlayerColor.None, troopA.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "The trophy-hall credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip for every command type this card can produce ---

        [TestMethod]
        public void PlaceSpyCommand_DtoRoundTrip_StillPlacesTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");
            scenario.PlayCard(card);

            var command = new PlaceSpyCommand(siteA.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as PlaceSpyCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);

            scenario.Dispatch(hydrated);

            Assert.Contains(red.Color, siteA.Spies);
        }

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            var card = scenario.GiveCard(PlayerColor.Red, "yan_c_bin");
            scenario.PlayCard(card);
            scenario.ClickTarget(null, siteA);

            var command = new AssassinateCommand(troopA.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, troopA.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }
    }
}
