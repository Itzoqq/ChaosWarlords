using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for rulebook p.12's Place-a-Spy exception: "if all your
    /// spies are already placed, you may return one of your own first, then place - or do
    /// nothing" (planning.txt TIER 1 item 17). This is an engine-level rule
    /// (PlaceSpyStrategy.HasValidTargets/SpySubsystem.HandlePlaceSpy), not tied to one card, so
    /// it's exercised here through the REAL "banshee" and "masters_of_sorcere" cards.json entries
    /// (mandatory PlaceSpy with a PendingSite-conditioned OnSuccess, and an IsOptional Choose-one
    /// PlaceSpy respectively) via a REAL CommandDispatcher, rather than a hand-typed effect tree.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class PlaceSpyEmptyBarracksScenarioTests
    {
        // --- Row 1/2: happy path, engine-level fix ---

        [TestMethod]
        public void PlayBanshee_EmptyBarracks_ReturnOwnSpyThenPlaceAtANewSite_ChainsOffThePlacementSiteNotTheReturnSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            red.SpiesInBarracks = 0;

            var returnSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            returnSite.AddSpy(red.Color); // Red's only spy - must be returned before anything else can happen.
            var placeSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != returnSite);
            placeSite.AddSpy(blue.Color); // Satisfies Banshee's own condition once Red places here.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Empty barracks must not make PlaceSpy a dead effect - Red has a spy on the board to return.");

            var returnCmd = scenario.ClickTarget(null, returnSite);
            Assert.IsInstanceOfType(returnCmd, typeof(ReturnSpyToPlaceCommand));
            Assert.DoesNotContain(red.Color, returnSite.Spies, "The spy must have actually been returned.");
            Assert.AreEqual(1, red.SpiesInBarracks, "Barracks replenished by exactly one.");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "PlaceSpy is still pending - returning was only the first half.");
            Assert.AreEqual(0, red.Power, "Nothing should have fired yet - the chain reads the PLACEMENT site, not the return site.");

            var placeCmd = scenario.ClickTarget(null, placeSite);
            Assert.IsInstanceOfType(placeCmd, typeof(PlaceSpyCommand));

            Assert.Contains(red.Color, placeSite.Spies, "Red's spy should now be at the new site.");
            Assert.AreEqual(0, red.SpiesInBarracks, "The replenished spy was immediately spent placing it.");
            Assert.AreEqual(3, red.Power, "Banshee's condition (another player's spy at the PLACEMENT site) is met - +3 Power should have fired.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayBanshee_EmptyBarracksAndNoOwnSpyAnywhere_SkipsPlaceSpyEffectCleanly()
        {
            // Confirms the fix didn't regress the pre-existing "genuinely nothing to do" case
            // (BansheeInfiltratorScenarioTests.PlayBanshee_NoSpiesInBarracks_SkipsPlaceSpyEffectCleanly
            // covers this same shape for its own card - kept here too since it's the OTHER half
            // of this exact rule).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0;
            // Deliberately no Red spy placed anywhere - the return-then-place exception doesn't apply.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No spy to place or return - the effect should skip cleanly.");
            Assert.AreEqual(0, red.Power);
        }

        [TestMethod]
        public void PlayMastersOfSorcere_EmptyBarracksAtCardPlayTime_StillOffersTheChooseOnePopupInsteadOfFallingStraightToTheAlternative()
        {
            // Masters of Sorcere's PlaceSpy half is IsOptional (a real "Choose one: Place 2
            // spies. Or, return one of your spies to gain 4 Power.") - with an empty barracks
            // AT CARD-PLAY TIME (not mid-repeat, unlike MastersOfSorcereScenarioTests' own
            // coverage of this card), the fix means PlaceSpy is still a genuinely choosable
            // branch as long as Red has an own spy on the board to return first, so the
            // confirmation popup must still fire rather than silently skipping straight to the
            // Alternative the way an unconditionally-invalid effect would.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0;
            var siteWithOwnSpy = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            siteWithOwnSpy.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup must still be offered - an empty barracks doesn't make PlaceSpy a dead branch while a spy remains on the board to return.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            var returnCmd = scenario.ClickTarget(null, siteWithOwnSpy);
            Assert.IsInstanceOfType(returnCmd, typeof(ReturnSpyToPlaceCommand));
            Assert.AreEqual(1, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Only the return half is done - a real placement (and, for this card, a 2nd repeat) is still owed.");
        }

        // --- Row 3: adversarial - stale/nonexistent target ---

        [TestMethod]
        public void ReturnSpyToPlaceCommand_StaleNonexistentSite_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0;
            var returnSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            returnSite.AddSpy(red.Color);
            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            var forgedCommand = new ReturnSpyToPlaceCommand(targetSiteId: -999, banshee.Id);

            scenario.AssertRejected(forgedCommand, "A nonexistent site must be rejected.");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row: adversarial - unmet resource precondition (barracks not actually empty) ---

        [TestMethod]
        public void ReturnSpyToPlaceCommand_BarracksNotActuallyEmpty_IsRejectedByDefenseInDepth()
        {
            // The click path (SpySubsystem.HandlePlaceSpy) never builds this command once the
            // barracks has a spy - this simulates a forged/direct dispatch bypassing that click
            // routing entirely, matching every other command's don't-trust-the-client rule.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var returnSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            returnSite.AddSpy(red.Color);
            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee); // Barracks is at its normal starting count here (> 0) - PlaceSpy resolves normally...
            scenario.ClickTarget(null, scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != returnSite));
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Sanity check: the ordinary (non-empty-barracks) path should have completed normally.");

            var forgedCommand = new ReturnSpyToPlaceCommand(returnSite.Id, banshee.Id);
            scenario.AssertRejected(forgedCommand, "No Place Spy effect is open anymore, and the barracks isn't empty either - must be rejected on both counts.");
        }

        // --- Row: adversarial - "wrong player's" spy ---

        [TestMethod]
        public void ReturnSpyToPlaceCommand_TargetingAnotherPlayersSpySite_IsRejected()
        {
            // "Return one of YOUR OWN spies" - a forged command naming a site where only an
            // opponent has a spy (none of the active player's own) must be rejected, even while
            // a genuine Place Spy effect is legitimately open for the active player.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            red.SpiesInBarracks = 0;

            var redSpySite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            redSpySite.AddSpy(red.Color); // Satisfies HasValidTargets so PlaceSpy actually opens.
            var blueSpySite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != redSpySite);
            blueSpySite.AddSpy(blue.Color); // Only Blue has a spy here - not eligible for Red to return.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            var forgedCommand = new ReturnSpyToPlaceCommand(blueSpySite.Id, banshee.Id);
            scenario.AssertRejected(forgedCommand, "Red has no own spy at Blue's site - must be rejected.");
            Assert.Contains(blue.Color, blueSpySite.Spies, "Blue's spy must survive untouched.");
        }

        // --- Row: double-dispatch/replay ---

        [TestMethod]
        public void ReturnSpyToPlaceCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0;
            var returnSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            returnSite.AddSpy(red.Color);
            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            var command = new ReturnSpyToPlaceCommand(returnSite.Id, banshee.Id);

            scenario.DispatchTwice(command);

            Assert.AreEqual(1, red.SpiesInBarracks, "The barracks must have been replenished exactly once, not twice.");
            Assert.DoesNotContain(red.Color, returnSite.Spies);
        }

        // --- Row: DTO round-trip ---

        [TestMethod]
        public void ReturnSpyToPlaceCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0;
            var returnSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            returnSite.AddSpy(red.Color);
            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            var command = new ReturnSpyToPlaceCommand(returnSite.Id, banshee.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as ReturnSpyToPlaceCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(red.Color, returnSite.Spies);
            Assert.AreEqual(1, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "PlaceSpy must still be open through a hydrated dispatch.");
        }
    }
}
