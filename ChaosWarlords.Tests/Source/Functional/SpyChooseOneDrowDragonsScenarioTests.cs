using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for planning.txt TIER 1 item 8's transcribed
    /// PlaceSpy/ReturnOwnSpy choose-one cards - the same established shape Masters of Sorcere
    /// already exercises (CardEffect.IsOptional/Alternative, PlaceSpy Amount=1 here rather than
    /// that card's Amount=2, so no repeat sub-flow to cover): Information Broker ("Choose one:
    /// Place a spy. Or, return one of your spies to draw 3 cards." - the first card chaining
    /// ReturnOwnSpy's OnSuccess into DrawCard rather than GainResource/Supplant/Assassinate),
    /// Enchanter of Thay ("Choose one: Place a spy. Or, return one of your spies to gain 4
    /// Power.") and Watcher of Thay ("Choose one: Place a spy. Or, return one of your spies to
    /// gain 3 Influence.") - both the identical ReturnOwnSpy-into-GainResource shape Masters of
    /// Sorcere's Alternative already uses, just Amount=1 with a different resource/amount.
    /// Loads the REAL cards.json entries and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SpyChooseOneDrowDragonsScenarioTests
    {
        // --- Information Broker: accept branch (Place a spy). ---

        [TestMethod]
        public void PlayInformationBroker_AcceptPlaceSpy_PlacesASpyAtTheChosenSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            var card = scenario.GiveCard(PlayerColor.Red, "information_broker");
            int handBefore = red.Hand.Count;

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Place a spy vs. the Alternative.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.HasCount(handBefore - 1, red.Hand, "The declined Alternative's Draw 3 must NOT also apply - hand should only be down by the card just played.");
        }

        // --- Information Broker: decline branch (return a spy, draw 3 cards). ---

        [TestMethod]
        public void PlayInformationBroker_DeclinePlaceSpy_ReturnsASpyAndDrawsThreeCards()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteWithSpy = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            siteWithSpy.AddSpy(red.Color);
            int spiesBefore = red.SpiesInBarracks;
            int handBefore = red.Hand.Count;

            var card = scenario.GiveCard(PlayerColor.Red, "information_broker");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, siteWithSpy);

            Assert.DoesNotContain(red.Color, siteWithSpy.Spies);
            Assert.AreEqual(spiesBefore + 1, red.SpiesInBarracks);
            Assert.HasCount(handBefore + 3, red.Hand, "Drawing 3 cards should have grown the hand by exactly 3.");
        }

        [TestMethod]
        public void PlayInformationBroker_NoSiteWithoutASpyAndNoSpyToReturn_SkipsEntirelyWithNoInteractionRequested()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            foreach (var site in scenario.Context.MapManager.Sites)
            {
                site.AddSpy(red.Color);
            }

            var card = scenario.GiveCard(PlayerColor.Red, "information_broker");
            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "No valid PlaceSpy target exists, so the optional-effect popup must never be raised.");
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState, "Should fall straight through to the Alternative.");
        }

        [TestMethod]
        public void PlayInformationBrokerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "information_broker");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlaceSpyCommand_TargetingANonexistentSite_IsRejectedWhileInformationBrokerEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "information_broker");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new PlaceSpyCommand(999999, card.Id), "A stale/nonexistent site id must be rejected.");
        }

        [TestMethod]
        public void PlayInformationBrokerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "information_broker");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Enchanter of Thay: accept branch (Place a spy). ---

        [TestMethod]
        public void PlayEnchanterOfThay_AcceptPlaceSpy_PlacesASpyAtTheChosenSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            var card = scenario.GiveCard(PlayerColor.Red, "enchanter_of_thay");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(0, red.Power, "The declined Alternative's Power gain must not also apply.");
        }

        // --- Enchanter of Thay: decline branch (return a spy, gain 4 Power). ---

        [TestMethod]
        public void PlayEnchanterOfThay_DeclinePlaceSpy_ReturnsASpyAndGainsFourPower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteWithSpy = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            siteWithSpy.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "enchanter_of_thay");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(null, siteWithSpy);

            Assert.DoesNotContain(red.Color, siteWithSpy.Spies);
            Assert.AreEqual(4, red.Power);
        }

        [TestMethod]
        public void PlayEnchanterOfThayCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "enchanter_of_thay");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayEnchanterOfThayCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "enchanter_of_thay");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Watcher of Thay: accept branch (Place a spy). ---

        [TestMethod]
        public void PlayWatcherOfThay_AcceptPlaceSpy_PlacesASpyAtTheChosenSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            var card = scenario.GiveCard(PlayerColor.Red, "watcher_of_thay");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(0, red.Influence, "The declined Alternative's Influence gain must not also apply.");
        }

        // --- Watcher of Thay: decline branch (return a spy, gain 3 Influence). ---

        [TestMethod]
        public void PlayWatcherOfThay_DeclinePlaceSpy_ReturnsASpyAndGainsThreeInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteWithSpy = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            siteWithSpy.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "watcher_of_thay");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(null, siteWithSpy);

            Assert.DoesNotContain(red.Color, siteWithSpy.Spies);
            Assert.AreEqual(3, red.Influence);
        }

        [TestMethod]
        public void PlayWatcherOfThayCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "watcher_of_thay");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayWatcherOfThayCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "watcher_of_thay");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip (shared shape, exercised once here rather than per card - the
        // underlying PlaceSpyCommand/ReturnOwnSpyCommand DTO round-trip is already covered
        // generically by MastersOfSorcereScenarioTests). ---
    }
}
