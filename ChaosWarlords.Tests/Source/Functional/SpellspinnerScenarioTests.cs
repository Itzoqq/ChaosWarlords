using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Spellspinner ("Choose one: Place a spy. Or, return one
    /// of your spies to supplant a troop at that spy's site.") - confirmed in planning.txt to be
    /// exactly Cloaker's shape (PlaceSpy optional, Alternative ReturnOwnSpy -> OnSuccess) with
    /// Supplant substituted for Assassinate, so this is pure data - zero new engine primitives,
    /// same site-scoping (ActionSystem.PendingSite, set by ReturnOwnSpyCommand, consulted by
    /// SupplantCommand.Validate()/SupplantStrategy.HasValidTargets) already proven by
    /// CloakerScenarioTests.cs and GrazztScenarioTests.cs. Loads the REAL "spellspinner" entry
    /// out of the REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SpellspinnerScenarioTests
    {
        /// <summary>
        /// Places a Red spy at <paramref name="site"/> and a Blue troop at one of its nodes, plus
        /// a separate Red troop adjacent to that node so Presence survives the spy leaving (same
        /// setup CloakerScenarioTests.cs/GrazztScenarioTests.cs use for the identical chain-link
        /// shape).
        /// </summary>
        private static MapNode SetupSpySiteWithSupplantableTroop(ChaosWarlords.Source.Entities.Actors.Player red, Site site)
        {
            var troopNode = site.NodesInternal[0];
            troopNode.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);
            var presenceNode = troopNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            presenceNode.Occupant = red.Color;
            return troopNode;
        }

        // --- Row 1: happy path, accept branch (Place a spy) ---

        [TestMethod]
        public void PlaySpellspinner_AcceptPlaceSpy_PlacesOneSpyAndNeverOffersReturnBranch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            int spiesBefore = red.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(spiesBefore - 1, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.HasCount(1, scenario.Interactions, "The Return branch's own popup must never have been raised.");
        }

        // --- Row 1b/2: happy path, decline branch, both the right and the wrong site ---

        [TestMethod]
        public void PlaySpellspinner_DeclineThenReturnOwnSpy_SupplantsOnlyAtThatSpysSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var troopA = SetupSpySiteWithSupplantableTroop(red, siteA);
            var troopB = siteB.NodesInternal[0];
            troopB.Occupant = blue.Color;
            int redSpiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");
            scenario.PlayCard(card);

            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Place a Spy vs. the Alternative.");
            scenario.RespondToLatestInteraction(accept: false); // Decline: use the Alternative.

            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, siteA);

            Assert.DoesNotContain(red.Color, siteA.Spies, "Spy should have left Site A.");
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Should chain straight into Supplant, scoped to Site A.");

            // Wrong site: Site B's troop is NOT where the spy was returned from.
            var rejected = scenario.ClickTarget(troopB, null);
            Assert.IsNull(rejected, "Supplanting at the wrong site should be rejected by the PendingSite guard.");
            Assert.AreEqual(blue.Color, troopB.Occupant, "Wrong-site troop must survive.");

            // Correct site: Site A's troop.
            scenario.ClickTarget(troopA, null);

            Assert.AreEqual(red.Color, troopA.Occupant, "Supplant deploys Red's own troop into the vacated space.");
            Assert.AreEqual(1, red.TrophyHall, "The assassinated half of Supplant sends Blue's troop to Red's trophy hall.");
            Assert.AreEqual(redSpiesBefore + 1, red.SpiesInBarracks, "The returned spy must have been replenished to Red's barracks.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3: no-valid-target fallback for BOTH branches ---

        [TestMethod]
        public void PlaySpellspinner_NothingToPlaceOrReturn_DoesNothingAtAllCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0; // No barracks stock - Place a Spy has no valid target.
            // Deliberately no spy placed anywhere - Return has no valid target either.
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "Neither branch has a valid target - no popup should ever be raised.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        /// <summary>
        /// Regression coverage for the site-scoping soft-lock fixed while building Green Dragon
        /// (SupplantStrategy.HasValidTargets used to ignore ActionSystem.PendingSite) - this
        /// chain-link shape (ReturnOwnSpy -> Supplant) is exactly Graz'zt's, but exercised here
        /// through Spellspinner's own real cards.json entry rather than a hand-typed card.
        /// </summary>
        [TestMethod]
        public void PlaySpellspinner_ReturnSpyFromASiteWithNoTroopWhileTheBoardHasOneElsewhere_ResolvesCleanlyInsteadOfStalling()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            siteA.AddSpy(red.Color); // Red's spy at siteA - no troop here at all.
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var nodeB = siteB.NodesInternal[0];
            nodeB.Occupant = blue.Color; // A globally-valid Supplant target, but at the WRONG site.
            nodeB.Neighbors.First(n => n.Occupant == PlayerColor.None).Occupant = red.Color;

            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy - use the Alternative.
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, siteA);

            Assert.DoesNotContain(red.Color, siteA.Spies, "The spy should still have been returned from siteA.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "siteA has no troop to Supplant - the chain must resolve cleanly, not stall in TargetingSupplant.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(blue.Color, siteB.NodesInternal[0].Occupant, "siteB's troop was never a legal target for this chain - must survive untouched.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlaySpellspinnerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "spellspinner");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Spellspinner should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, and Supplant's site-scoping defense-in-depth ---

        [TestMethod]
        public void ReturnOwnSpyCommand_ForASiteWithNoSpyThere_IsRejectedDuringSpellspinnersChain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSpySiteWithSupplantableTroop(red, site);
            var untouchedSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != site); // No spy here.
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy.

            var forgedCommand = new ReturnOwnSpyCommand(untouchedSite.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A site with no Red spy must be rejected as a return target.");
        }

        [TestMethod]
        public void SupplantCommand_TargetingTheWrongSite_IsRejectedByValidateDefenseInDepth()
        {
            // Bypasses the input-layer PendingSite guard entirely - proves SupplantCommand.
            // Validate()'s own IsAtRequiredSite check is the real defense against a forged
            // command, not just ActionInputController.HandleSupplant's UI-layer guard.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troopA = SetupSpySiteWithSupplantableTroop(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var troopB = SetupSpySiteWithSupplantableTroop(red, siteB);
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy.
            scenario.ClickTarget(null, siteA); // Returns from Site A - PendingSite now Site A.

            var forgedCommand = new SupplantCommand(troopB.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "Supplanting at Site B's troop while PendingSite is Site A must be rejected.");
            Assert.AreEqual(PlayerColor.Blue, troopB.Occupant);

            scenario.ClickTarget(troopA, null); // The correct site still works.
            Assert.AreEqual(red.Color, troopA.Occupant);
        }

        // --- Row 7: double-dispatch/replay, for every command type this card can produce ---

        [TestMethod]
        public void PlaySpellspinnerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        [TestMethod]
        public void ReturnOwnSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy.
            scenario.DispatchTwice(new ReturnOwnSpyCommand(site.Id, card.Id));

            Assert.DoesNotContain(red.Color, site.Spies, "The spy must have been returned exactly once.");
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "The replay must not have advanced past the pending Supplant.");
        }

        [TestMethod]
        public void SupplantCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy.
            scenario.ClickTarget(null, site);
            scenario.DispatchTwice(new SupplantCommand(troop.Id, card.Id));

            Assert.AreEqual(red.Color, troop.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "The trophy-hall credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip for every command type this card can produce ---

        [TestMethod]
        public void ReturnOwnSpyCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy.

            var command = new ReturnOwnSpyCommand(site.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as ReturnOwnSpyCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(red.Color, site.Spies);
        }

        [TestMethod]
        public void SupplantCommand_DtoRoundTrip_StillSupplantsThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "spellspinner");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place a Spy.
            scenario.ClickTarget(null, site);

            var command = new SupplantCommand(troop.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as SupplantCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, troop.Occupant);
        }
    }
}
