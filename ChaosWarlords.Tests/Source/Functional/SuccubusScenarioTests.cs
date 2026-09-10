using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Succubus ("You may devour a card from your hand to
    /// place a spy, then assassinate a troop at that spy's site.") - confirmed in planning.txt
    /// to be exactly Cloaker/Yan-C-Bin's site-scoped PlaceSpy-&gt;OnSuccess:Assassinate shape,
    /// prefixed by an optional Devour(Hand) cost with no Alternative (Market Corruptor's
    /// established "you may pay this cost, decline grants nothing" shape). Loads the REAL
    /// "succubus" entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SuccubusScenarioTests
    {
        [TestMethod]
        public void PlaySuccubus_AcceptDevourThenPlaceSpy_AssassinatesOnlyAtThatSitesTroop()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            var troopB = siteB.NodesInternal[0];
            troopB.Occupant = blue.Color;

            scenario.PlayCard(succubus);
            Assert.HasCount(1, scenario.Interactions, "The optional Devour-cost popup should fire.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingDevourHand, scenario.Context.ActionSystem.CurrentState);
            scenario.SelectDevourCard(noble);

            Assert.IsFalse(red.Hand.Contains(noble), "Noble should have been devoured.");
            Assert.AreEqual(CardLocation.Void, noble.Location);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, siteA);

            Assert.Contains(red.Color, siteA.Spies);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Should chain straight into Assassinate, scoped to the just-spied site.");

            // Wrong site: Site B's troop is NOT where the spy was placed.
            var rejected = scenario.ClickTarget(troopB, null);
            Assert.IsNull(rejected, "Assassinating at the wrong site should be rejected by the PendingSite guard.");
            Assert.AreEqual(blue.Color, troopB.Occupant, "Wrong-site troop must survive.");

            scenario.ClickTarget(troopA, null);

            Assert.AreEqual(PlayerColor.None, troopA.Occupant, "Correct-site troop should be assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlaySuccubus_DeclineDevour_GrantsNothingAtAll()
        {
            // Unlike Wight/Cultist of Myrkul, there's no Alternative here - the real card is a
            // plain "you may", not a "choose one". Declining must leave the player with nothing.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble"); // A real Devour target exists.
            int spiesBefore = red.SpiesInBarracks;

            scenario.PlayCard(succubus);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.Contains(noble, red.Hand, "Declining must leave the candidate Devour target untouched.");
            Assert.AreEqual(spiesBefore, red.SpiesInBarracks, "Declining a plain optional effect (no Alternative) must not place a spy either.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlaySuccubus_WithEmptyHand_SkipsThePopupEntirely()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus"); // Only card in hand.

            scenario.PlayCard(succubus);

            Assert.IsEmpty(scenario.Interactions, "No valid Devour target (empty hand besides Succubus itself) means no popup.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Should fall straight through to Normal, not stall waiting for an impossible click.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlaySuccubus_AcceptWouldChainIntoAnUnreachablePlaceSpy_SkipsThePopupEntirely()
        {
            // Red already has a spy at EVERY site, so PlaceSpy (Devour's OnSuccess child) has
            // no valid target anywhere on the board. Devour's only purpose here IS enabling
            // that chain (same shape as Wight, not Graz'zt's ChainedRepeatCount rounds - see
            // CardEffect.SkipUnreachableOnSuccessCheck's doc comment) - ActionExecutionEngine.
            // HasUnreachableOnSuccess's lookahead should skip the confirmation popup entirely
            // rather than asking the player to devour a card for a chain that can't fire.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            foreach (var site in scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0))
            {
                site.AddSpy(red.Color);
            }
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(succubus);

            Assert.IsEmpty(scenario.Interactions, "The OnSuccess PlaceSpy is unreachable everywhere - no popup should ever be raised.");
            Assert.Contains(noble, red.Hand, "Nothing should have been devoured.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlaySuccubus_AcceptDevourAndPlaceSpyButNoTroopAtThatSite_ResolvesCleanlyInsteadOfStalling()
        {
            // HasUnreachableOnSuccess only looks ONE level into the chain - it validates
            // PlaceSpy (Devour's immediate OnSuccess) is reachable, never PlaceSpy's own
            // nested OnSuccess (Assassinate). Placing a spy has value on its own (same
            // reasoning as Graz'zt's "returning a spy has value independent of the chained
            // Supplant") - so the popup still fires and the spy still gets placed here, even
            // though the site it lands on turns out to have no troop to Assassinate.
            // ActionExecutionEngine.TryEnterTargetingForRequiredEffect re-validates Assassinate
            // fresh right before entering its targeting state and must resolve this as a clean
            // no-op, not a stall (see YanCBinScenarioTests' identical-shape regression test).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            // Deliberately no troop anywhere at siteA - the Assassinate half has nothing to hit.

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);
            scenario.ClickTarget(null, siteA);

            Assert.Contains(red.Color, siteA.Spies, "The spy should still have been placed.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "siteA has no troop to Assassinate - the chain must resolve cleanly, not stall in TargetingAssassinate.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlaySuccubusCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var succubus = scenario.GiveCard(PlayerColor.Blue, "succubus");

            scenario.AssertRejected(new PlayCardCommand(succubus));

            Assert.Contains(succubus, blue.Hand, "Succubus should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, and Assassinate's site-scoping defense-in-depth ---

        [TestMethod]
        public void DevourCardCommand_ForACardNoLongerInHand_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingDevourHand, scenario.Context.ActionSystem.CurrentState);

            var goneCard = CardFactory.CreateNoble(scenario.Context.Random);
            // Never added to Red's hand - simulates a target that has already left it.

            var command = new DevourCardCommand(goneCard) { SourceCard = succubus };
            scenario.AssertRejected(command);

            Assert.AreEqual(ActionState.TargetingDevourHand, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real selection.");
        }

        [TestMethod]
        public void AssassinateCommand_TargetingTheWrongSite_IsRejectedByValidateDefenseInDepth()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;
            var troopB = siteB.NodesInternal[0];
            troopB.Occupant = blue.Color;
            var presenceNodeB = troopB.Neighbors.First(n => n.Occupant == PlayerColor.None);
            presenceNodeB.Occupant = red.Color; // Genuine Presence at siteB too - proves rejection is about scoping, not Presence.

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);
            scenario.ClickTarget(null, siteA); // Places the spy at Site A - PendingSite now Site A.
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(troopB.Id, succubus.Id);
            scenario.AssertRejected(forgedCommand, "Assassinating at Site B's troop while PendingSite is Site A must be rejected.");
            Assert.AreEqual(blue.Color, troopB.Occupant);

            scenario.ClickTarget(troopA, null);
            Assert.AreEqual(PlayerColor.None, troopA.Occupant);
        }

        [TestMethod]
        public void PlaceSpyCommand_ForASiteAlreadySpiedByRed_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            siteA.AddSpy(red.Color); // Already spied - not a valid PlaceSpy target.

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);

            var forgedCommand = new PlaceSpyCommand(siteA.Id, succubus.Id);
            scenario.AssertRejected(forgedCommand, "A site Red already has a spy at must be rejected as a PlaceSpy target.");
        }

        // --- Row 7: double-dispatch/replay, for every command type this card can produce ---

        [TestMethod]
        public void PlaySuccubusCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.DispatchTwice(new PlayCardCommand(succubus));

            Assert.HasCount(1, scenario.Interactions, "The optional-effect popup should have been raised exactly once, not twice.");
        }

        [TestMethod]
        public void DevourCardCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.DispatchTwice(new DevourCardCommand(noble) { SourceCard = succubus });

            Assert.AreEqual(CardLocation.Void, noble.Location, "The card must have been devoured exactly once.");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "The replay must not have advanced past the pending PlaceSpy.");
        }

        [TestMethod]
        public void PlaceSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            siteA.NodesInternal[0].Occupant = blue.Color; // A troop to Assassinate, so the chain has somewhere to land.

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);
            scenario.DispatchTwice(new PlaceSpyCommand(siteA.Id, succubus.Id));

            Assert.HasCount(1, siteA.Spies.Where(c => c == red.Color), "The spy must have been placed exactly once.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The replay must not have advanced past the pending Assassinate.");
        }

        [TestMethod]
        public void AssassinateCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);
            scenario.ClickTarget(null, siteA);
            scenario.DispatchTwice(new AssassinateCommand(troopA.Id, succubus.Id));

            Assert.AreEqual(PlayerColor.None, troopA.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "The trophy-hall credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip for every command type this card can produce ---

        [TestMethod]
        public void DevourCardCommand_DtoRoundTrip_StillDevoursThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);

            var command = new DevourCardCommand(noble) { SourceCard = succubus };
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as DevourCardCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.CardRuntimeId, hydrated!.CardRuntimeId);

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(noble, red.Hand);
            Assert.AreEqual(CardLocation.Void, noble.Location);
        }

        [TestMethod]
        public void PlaceSpyCommand_DtoRoundTrip_StillPlacesTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);

            var command = new PlaceSpyCommand(siteA.Id, succubus.Id);
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
            var succubus = scenario.GiveCard(PlayerColor.Red, "succubus");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troopA = siteA.NodesInternal[0];
            troopA.Occupant = blue.Color;

            scenario.PlayCard(succubus);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(noble);
            scenario.ClickTarget(null, siteA);

            var command = new AssassinateCommand(troopA.Id, succubus.Id);
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
