using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Masters of Sorcere ("Choose one: Place 2 spies. Or,
    /// return one of your spies to gain 4 Power.") - the first shipped card using PlaceSpy's
    /// repeat support (IEffectStrategy.SupportsRepeat, added for this card - see
    /// PlaceSpyStrategy.cs) combined with the established Choose-one shape (CardEffect.
    /// IsOptional/Alternative, same as Cloaker/Wight). Loads the REAL "masters_of_sorcere"
    /// entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MastersOfSorcereScenarioTests
    {
        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayMastersOfSorcere_AcceptPlaceSpy_PlacesTwoSpiesAtTwoDifferentSites()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int spiesBefore = red.SpiesInBarracks;
            var sites = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).Take(2).ToList();
            Assert.HasCount(2, sites, "Setup check: at least 2 sites without Red's spy must exist.");

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);

            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Place 2 spies vs. the Alternative.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, sites[0]);
            Assert.Contains(red.Color, sites[0].Spies);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "One more spy is still owed.");
            Assert.IsNotEmpty(scenario.Context.ActionSystem.ExecutionStack, "The PlaceSpy effect must still be on the stack, waiting for the 2nd site.");

            scenario.ClickTarget(null, sites[1]);
            Assert.Contains(red.Color, sites[1].Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(spiesBefore - 2, red.SpiesInBarracks);
            Assert.AreEqual(0, red.Power, "The Alternative's Power gain must NOT also apply - Choose-one mutual exclusivity.");
        }

        // --- Row 2: Choose-one mutual exclusivity, the OTHER direction ---

        [TestMethod]
        public void PlayMastersOfSorcere_DeclinePlaceSpy_ReturnsASpyAndGainsFourPower_NotPlacingAnySpy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteWithSpy = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            siteWithSpy.AddSpy(red.Color); // Setup only - gives the Alternative something to return.
            int spiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, siteWithSpy);

            Assert.DoesNotContain(red.Color, siteWithSpy.Spies, "The spy should have been returned.");
            Assert.AreEqual(spiesBefore + 1, red.SpiesInBarracks);
            Assert.AreEqual(4, red.Power, "The Alternative's +4 Power should have applied.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 3: no-valid-target fallback, plus the "fewer than requested targets exist" edge case ---

        [TestMethod]
        public void PlayMastersOfSorcere_OnlyOneSiteWithoutASpyAvailable_PlacesOneSpyAndResolvesWithoutASecondClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var allSites = scenario.Context.MapManager.Sites.ToList();
            // Occupy every site except one with Red's own spy, so only 1 valid PlaceSpy target remains.
            foreach (var site in allSites.Skip(1))
            {
                site.AddSpy(red.Color);
            }
            var onlyTarget = allSites[0];

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, onlyTarget);

            Assert.Contains(red.Color, onlyTarget.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "With no more valid sites, the effect must resolve instead of waiting for an impossible 2nd.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayMastersOfSorcere_OnlyOneSpyLeftInBarracks_PlacesOneSpyAndResolvesWithoutASecondClick()
        {
            // The OTHER way the repeat can run out early: plenty of valid, unoccupied sites
            // remain (unlike the test above), but the barracks itself only has 1 spy left -
            // PlaceSpyStrategy.HasValidTargets is a compound "SpiesInBarracks > 0 AND a valid
            // site exists" check, and this is the half that test doesn't cover.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 1;
            var sites = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).ToList();
            Assert.IsGreaterThanOrEqualTo(2, sites.Count, "Setup check: at least 2 valid sites must remain available.");

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, sites[0]);

            Assert.Contains(red.Color, sites[0].Spies);
            Assert.AreEqual(0, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "With the barracks empty, the effect must resolve instead of waiting for an impossible 2nd - even though other valid sites still exist.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayMastersOfSorcere_NoSiteWithoutASpyAndNoSpyToReturn_SkipsEntirelyWithNoInteractionRequested()
        {
            // Neither branch has a valid target at all: every site already has Red's spy (no
            // PlaceSpy target) - but that also means Red HAS spies out there, so this scenario
            // instead proves the "no PlaceSpy target -> straight to Alternative, no popup"
            // behavior (matching DevourFromInnerCircleIntegrationTests.cs's established
            // "optional effect with zero valid targets never raises a popup" precedent), landing
            // on a real ReturnOwnSpy target instead of fizzling to nothing.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            foreach (var site in scenario.Context.MapManager.Sites)
            {
                site.AddSpy(red.Color);
            }

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "No valid PlaceSpy target exists, so the optional-effect popup must never be raised.");
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState, "Should fall straight through to the Alternative.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayMastersOfSorcereCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "masters_of_sorcere");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Masters of Sorcere should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions);
        }

        // --- Row 5: stale/illegal target for the SECOND PlaceSpy repeat specifically ---

        [TestMethod]
        public void PlaceSpyCommand_TargetingASiteRedAlreadyHasASpyAt_IsRejectedForTheSecondMastersOfSorcereTarget()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).Take(2).ToList();

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, sites[0]);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Still 1 more spy owed.");

            // sites[0] now already has Red's spy - re-targeting it for the SECOND repeat must be
            // rejected exactly like any other already-occupied (by Red) site would be.
            var forgedCommand = new PlaceSpyCommand(sites[0].Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A site Red already has a spy at must be rejected as a target for the 2nd repeat.");

            Assert.Contains(red.Color, sites[0].Spies);
            Assert.DoesNotContain(red.Color, sites[1].Spies, "sites[1] must remain untouched by the rejected re-target.");
        }

        [TestMethod]
        public void PlaceSpyCommand_TargetingANonexistentSite_IsRejectedWhileMastersOfSorcereEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new PlaceSpyCommand(999999, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent site id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlaceSpyCommand_DispatchedTwiceAgainstTheSameFirstSite_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).Take(2).ToList();
            int spiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            scenario.DispatchTwice(new PlaceSpyCommand(sites[0].Id, card.Id));

            Assert.AreEqual(spiesBefore - 1, red.SpiesInBarracks, "Exactly 1 spy should have been placed, not 2.");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed - exactly 1 more site should still be owed.");

            scenario.ClickTarget(null, sites[1]);
            Assert.AreEqual(spiesBefore - 2, red.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayMastersOfSorcereCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void PlaceSpyCommand_DtoRoundTrip_StillPlacesTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            var command = new PlaceSpyCommand(site.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as PlaceSpyCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a PlaceSpyCommand.");
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);

            scenario.Dispatch(hydrated);

            Assert.Contains(red.Color, site.Spies);
        }

        [TestMethod]
        public void ReturnOwnSpyCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteWithSpy = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            siteWithSpy.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "masters_of_sorcere");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);

            var command = new ReturnOwnSpyCommand(siteWithSpy.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as ReturnOwnSpyCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a ReturnOwnSpyCommand.");

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(red.Color, siteWithSpy.Spies);
            Assert.AreEqual(4, red.Power);
        }
    }
}
