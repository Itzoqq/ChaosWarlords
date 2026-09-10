using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Aboleth ("Choose one: Place 2 spies. Or, draw a card
    /// for each spy you have on the board.") - the same PlaceSpy-repeat/Choose-one shape Masters
    /// of Sorcere already established, but with a plain, non-targeting DrawCard as the
    /// Alternative instead of ReturnOwnSpy - the first shipped card to combine DrawCard with the
    /// new DynamicAmountSource.SpiesOnBoard case (CardEffectApplier.ApplyDrawCard, generalized
    /// to route through ResolveAmount the same way ApplyGainResource already did; DrawCard
    /// itself was already reachable in production via Grimlock's ReactiveDiscardEffect, just
    /// never with a dynamic amount before now). Loads the REAL "aboleth" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class AbolethScenarioTests
    {
        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayAboleth_AcceptPlaceSpy_PlacesTwoSpiesAtTwoDifferentSitesAndDrawsNothing()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int handSizeBefore = red.Hand.Count;
            var sites = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).Take(2).ToList();
            Assert.HasCount(2, sites, "Setup check: at least 2 sites without Red's spy must exist.");

            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");
            scenario.PlayCard(card);

            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Place 2 spies vs. the Alternative.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, sites[0]);
            scenario.ClickTarget(null, sites[1]);

            Assert.Contains(red.Color, sites[0].Spies);
            Assert.Contains(red.Color, sites[1].Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
            // Choose-one mutual exclusivity: accepting Place 2 spies must NOT also draw cards -
            // Aboleth itself being added-then-played nets to zero, so the hand size is unchanged.
            Assert.HasCount(handSizeBefore, red.Hand, "No cards should have been drawn.");
        }

        // --- Row 1/2: decline branch - the headline dynamic-amount behavior + mutual exclusivity ---

        [TestMethod]
        public void PlayAboleth_DeclineWithThreeSpiesAlreadyOnBoard_DrawsThreeCardsAndPlacesNoSpy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sitesWithSpies = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).Take(3).ToList();
            Assert.HasCount(3, sitesWithSpies, "Setup check: at least 3 sites must exist.");
            foreach (var site in sitesWithSpies)
            {
                site.AddSpy(red.Color); // Setup only - not going through a command.
            }
            for (int i = 0; i < 3; i++) // Enough deck depth to actually draw 3.
            {
                red.DeckManager.AddToTop(scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!);
            }
            int handSizeBeforeDecline = red.Hand.Count;
            int spiesInBarracksBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");
            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false); // Decline: use the Alternative.

            Assert.HasCount(handSizeBeforeDecline + 3, red.Hand, "3 spies already on the board -> draw exactly 3 cards.");
            Assert.AreEqual(spiesInBarracksBefore, red.SpiesInBarracks, "The Place-2-spies half must NOT also have fired.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayAboleth_DeclineWithNoSpiesOnBoard_DrawsZeroCards()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int handSizeBefore = red.Hand.Count;

            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.HasCount(handSizeBefore, red.Hand, "0 spies on the board -> 0 cards drawn; Aboleth itself being added-then-played nets to zero.");
        }

        // --- Row 3: no-valid-target fallback for the PlaceSpy half ---

        [TestMethod]
        public void PlayAboleth_NoSiteWithoutASpyAvailable_SkipsThePopupAndDrawsInstead()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            foreach (var site in scenario.Context.MapManager.Sites)
            {
                site.AddSpy(red.Color); // Every site already has Red's spy - no PlaceSpy target.
            }
            int spyCount = scenario.Context.MapManager.Sites.Count;
            for (int i = 0; i < spyCount; i++)
            {
                red.DeckManager.AddToTop(scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!);
            }
            int handSizeBefore = red.Hand.Count;

            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");
            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "No valid PlaceSpy target exists, so the optional-effect popup must never be raised.");
            Assert.HasCount(handSizeBefore + spyCount, red.Hand, "Should fall straight through to drawing 1 card per already-placed spy (Aboleth itself nets to zero).");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayAbolethCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "aboleth");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Aboleth should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions);
        }

        // --- Row 5: stale/illegal target for the SECOND PlaceSpy repeat ---

        [TestMethod]
        public void PlaceSpyCommand_TargetingANonexistentSite_IsRejectedWhileAbolethEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new PlaceSpyCommand(999999, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent site id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayAbolethCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        [TestMethod]
        public void PlaceSpyCommand_DispatchedTwiceAgainstTheSameFirstSite_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => !s.Spies.Contains(red.Color)).Take(2).ToList();
            int spiesBefore = red.SpiesInBarracks;

            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            scenario.DispatchTwice(new PlaceSpyCommand(sites[0].Id, card.Id));

            Assert.AreEqual(spiesBefore - 1, red.SpiesInBarracks, "Exactly 1 spy should have been placed, not 2.");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed - exactly 1 more site should still be owed.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void PlaceSpyCommand_DtoRoundTrip_StillPlacesTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));

            var card = scenario.GiveCard(PlayerColor.Red, "aboleth");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            var command = new PlaceSpyCommand(site.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as PlaceSpyCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a PlaceSpyCommand.");
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);

            scenario.Dispatch(hydrated);

            Assert.Contains(red.Color, site.Spies);
        }
    }
}
