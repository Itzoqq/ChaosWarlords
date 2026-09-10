using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Graz'zt ("Choose one: Place 2 spies. Or, return any
    /// number of your spies, then Supplant a troop at each of the returned spies' sites.") - the
    /// first shipped card using CardEffect.ChainedRepeatCount (see ChainedRepeatChainTests.cs for
    /// engine-level coverage of that primitive itself). Loads the REAL "grazzt" entry out of the
    /// REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class GrazztScenarioTests
    {
        /// <summary>
        /// Places a Red spy at <paramref name="site"/> and a Blue troop at one of its nodes,
        /// plus a separate Red troop adjacent to that node so Presence survives the spy leaving
        /// (same setup CloakerScenarioTests.cs/ChainedRepeatChainTests.cs use for the identical
        /// chain-link shape).
        /// </summary>
        private static MapNode SetupSpySiteWithSupplantableTroop(Player red, Site site)
        {
            var troopNode = site.NodesInternal[0];
            troopNode.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);
            var presenceNode = troopNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            presenceNode.Occupant = red.Color;
            return troopNode;
        }

        // --- Row 1: happy path, accept branch (Place 2 spies) ---

        [TestMethod]
        public void PlayGrazzt_AcceptPlaceSpies_PlacesTwoSpiesAndNeverOffersReturnBranch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var siteB = scenario.Context.MapManager.Sites.First(s => s.Name == "Shadow Market");
            int spiesBefore = red.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, siteA);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");
            scenario.ClickTarget(null, siteB);

            Assert.Contains(red.Color, siteA.Spies);
            Assert.Contains(red.Color, siteB.Spies);
            Assert.AreEqual(spiesBefore - 2, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.HasCount(1, scenario.Interactions, "The Return branch's own popup must never have been raised.");
        }

        // --- Row 1b/2: happy path, decline branch - single round ---

        [TestMethod]
        public void PlayGrazzt_DeclineThenReturnOneSpyAndSupplant_TroopBecomesRedsAtThatSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            int redSpiesBefore = red.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            Assert.HasCount(2, scenario.Interactions, "Declining Place 2 spies must immediately raise round 1's own 'return a spy?' popup.");
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Chains straight into Supplant, scoped to this site.");
            scenario.ClickTarget(troop, null);

            Assert.AreEqual(red.Color, troop.Occupant);
            Assert.AreEqual(redSpiesBefore + 1, red.SpiesInBarracks);
            Assert.AreEqual(1, red.TrophyHall, "The assassinated half of Supplant sends Blue's troop to RED's trophy hall.");
            Assert.DoesNotContain(red.Color, site.Spies);

            // Round 2's own ReturnOwnSpy has no valid target now (nothing left to return) -
            // it self-resolves with no further popup, ending the whole sequence.
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3: no-valid-target fallback for BOTH branches ---

        [TestMethod]
        public void PlayGrazzt_NothingToPlaceOrReturn_DoesNothingAtAllCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0; // No barracks stock - Place 2 Spies has no valid target.
            // Deliberately no spy placed anywhere - Return has no valid target either.
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "Neither branch has a valid target - no popup should ever be raised.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayGrazztCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "grazzt");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Graz'zt should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, and the NEW Supplant site-scoping defense ---

        [TestMethod]
        public void ReturnOwnSpyCommand_ForASiteWithNoSpyThere_IsRejectedDuringGrazztsChain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            SetupSpySiteWithSupplantableTroop(red, site);
            var untouchedSite = scenario.Context.MapManager.Sites.First(s => s.Name == "Shadow Market"); // No spy here.
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.

            var forgedCommand = new ReturnOwnSpyCommand(untouchedSite.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A site with no Red spy must be rejected as a return target.");
        }

        [TestMethod]
        public void SupplantCommand_TargetingTheWrongSite_IsRejectedByValidateDefenseInDepth()
        {
            // Bypasses the input-layer PendingSite guard entirely - proves SupplantCommand.
            // Validate()'s own IsAtRequiredSite check (mirrors AssassinateCommand.
            // IsAtRequiredSite exactly - same PendingSite field, same site-scoping contract) is
            // the real defense against a forged command, not just
            // ActionInputController.HandleSupplant's UI-layer guard.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troopA = SetupSpySiteWithSupplantableTroop(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.Name == "Shadow Market");
            var troopB = SetupSpySiteWithSupplantableTroop(red, siteB);
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.
            scenario.ClickTarget(null, siteA); // Returns from Site A - PendingSite now Site A.

            var forgedCommand = new SupplantCommand(troopB.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "Supplanting at Site B's troop while PendingSite is Site A must be rejected.");
            Assert.AreEqual(PlayerColor.Blue, troopB.Occupant);

            scenario.ClickTarget(troopA, null); // The correct site still works.
            Assert.AreEqual(red.Color, troopA.Occupant);
        }

        // --- Row 7: double-dispatch/replay, for both command types this branch produces ---

        [TestMethod]
        public void ReturnOwnSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.
            scenario.DispatchTwice(new ReturnOwnSpyCommand(site.Id, card.Id));

            Assert.DoesNotContain(red.Color, site.Spies, "The spy must have been returned exactly once.");
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "The replay must not have advanced past the pending Supplant.");
        }

        [TestMethod]
        public void SupplantCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.
            scenario.ClickTarget(null, site);
            scenario.DispatchTwice(new SupplantCommand(troop.Id, card.Id));

            Assert.AreEqual(red.Color, troop.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "The trophy-hall credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip for both command types ---

        [TestMethod]
        public void ReturnOwnSpyCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.

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
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline Place 2 spies.
            scenario.RespondToLatestInteraction(accept: true); // Accept round 1: return a spy.
            scenario.ClickTarget(null, site);

            var command = new SupplantCommand(troop.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as SupplantCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, troop.Occupant);
        }

        // --- Double-dispatch on the card play itself ---

        [TestMethod]
        public void PlayGrazztCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "grazzt");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }
    }
}
