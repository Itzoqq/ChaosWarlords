using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Green Wyrmling ("Place a spy. If another player's troop
    /// is at that site, gain 2 Influence.") - planning.txt TIER 1 item 8. The same
    /// ConditionType.OpponentPresentAtSite(Troop)-gated PlaceSpy OnSuccess shape Infiltrator
    /// already exercises (BansheeInfiltratorScenarioTests.cs), just a different resource/amount.
    /// Loads the REAL "green_wyrmling" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class GreenWyrmlingScenarioTests
    {
        [TestMethod]
        public void PlayGreenWyrmling_OpponentTroopAtTargetSite_PlacesSpyAndGainsTwoInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.NodesInternal[0].Occupant = blue.Color;

            var card = scenario.GiveCard(PlayerColor.Red, "green_wyrmling");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies, "Red's spy should have been placed regardless of the bonus condition.");
            Assert.AreEqual(2, red.Influence, "Another player's troop is present - the +2 Influence bonus should have fired.");
        }

        [TestMethod]
        public void PlayGreenWyrmling_NoOpponentPresenceAtSite_PlacesSpyButNoInfluenceGain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);

            var card = scenario.GiveCard(PlayerColor.Red, "green_wyrmling");
            scenario.PlayCard(card);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies, "PlaceSpy is unconditional - it must still succeed even though the bonus won't fire.");
            Assert.AreEqual(0, red.Influence, "No opponent presence at the site - the bonus must not fire.");
        }

        [TestMethod]
        public void PlayGreenWyrmling_OpponentSpyButNoOpponentTroopAtSite_NoInfluenceGain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(blue.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "green_wyrmling");
            scenario.PlayCard(card);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(0, red.Influence, "An opponent spy (no opponent troop) must not satisfy the Troop-gated condition.");
        }

        [TestMethod]
        public void PlayGreenWyrmling_NoSpiesInBarracks_SkipsPlaceSpyEffectCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            red.SpiesInBarracks = 0;
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.NodesInternal[0].Occupant = blue.Color;

            var card = scenario.GiveCard(PlayerColor.Red, "green_wyrmling");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No spies to place - the effect should skip cleanly rather than entering TargetingPlaceSpy.");
            Assert.AreEqual(0, red.Influence);
        }

        [TestMethod]
        public void PlayGreenWyrmlingCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "green_wyrmling");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlaceSpyCommand_TargetingNonexistentSite_IsRejectedWhileGreenWyrmlingEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "green_wyrmling");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new PlaceSpyCommand(999999, card.Id));
        }

        [TestMethod]
        public void PlaceSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "green_wyrmling");
            scenario.PlayCard(card);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var command = scenario.Context.ActionSystem.HandleTargetClick(null, site) as PlaceSpyCommand;
            Assert.IsNotNull(command, "Setup check: the click should have produced a real PlaceSpyCommand.");

            scenario.DispatchTwice(command!);

            Assert.HasCount(1, site.Spies.Where(c => c == red.Color), "Red's spy should be at the site exactly once, not twice.");
        }
    }
}
