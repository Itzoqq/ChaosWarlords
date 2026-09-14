using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Rath Modar ("Draw 2 cards. Place a spy.") - planning.txt
    /// TIER 1 item 8. Two independent mandatory effects: an automatic DrawCard(2) (no targeting
    /// at all) followed by a mandatory single-site PlaceSpy, the same shape Drow Spy Master's
    /// "Place a Spy" already exercises but with the draw stacked in front of it. Loads the REAL
    /// "rath_modar" entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class RathModarScenarioTests
    {
        // --- Row 1: positive/happy path ---

        [TestMethod]
        public void PlayRathModar_DrawsTwoCardsAndPlacesASpyAtTheChosenSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int handBefore = red.Hand.Count;
            var site = scenario.Context.MapManager.Sites.First(s => !s.Spies.Contains(red.Color));
            var card = scenario.GiveCard(PlayerColor.Red, "rath_modar");

            scenario.PlayCard(card);

            Assert.HasCount(handBefore + 2, red.Hand, "Drawing 2 cards should have grown the hand by exactly 2 (before this card itself moves to Played).");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 3: no-valid-target fallback (no spies left in barracks) ---

        [TestMethod]
        public void PlayRathModar_NoSpiesInBarracks_StillDrawsButSkipsPlaceSpyCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0;
            int handBefore = red.Hand.Count;
            var card = scenario.GiveCard(PlayerColor.Red, "rath_modar");

            scenario.PlayCard(card);

            Assert.HasCount(handBefore + 2, red.Hand, "The independent Draw effect must still resolve even though PlaceSpy has nothing to do.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayRathModarCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            int handBefore = blue.Hand.Count;
            var card = scenario.GiveCard(PlayerColor.Blue, "rath_modar");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Rath Modar should still be in Blue's hand - the command must not have executed.");
            Assert.HasCount(handBefore + 1, blue.Hand, "Only the GiveCard setup added a card - nothing should have been drawn.");
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void PlaceSpyCommand_TargetingANonexistentSite_IsRejectedWhileRathModarEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "rath_modar");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new PlaceSpyCommand(999999, card.Id), "A stale/nonexistent site id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayRathModarCommand_DispatchedTwice_SecondDispatchIsRejectedAndDoesNotDoubleDraw()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int handBefore = red.Hand.Count;
            var card = scenario.GiveCard(PlayerColor.Red, "rath_modar");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(handBefore + 2, red.Hand, "Should have drawn exactly once (2 cards), not twice.");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Should still be waiting for exactly the one site click the first play triggered.");
        }
    }
}
