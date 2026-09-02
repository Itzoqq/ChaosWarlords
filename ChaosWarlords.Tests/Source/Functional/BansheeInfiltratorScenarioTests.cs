using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Banshee and Infiltrator (planning.txt TIER 2 #1) - both
    /// "Place a spy. If another player's [spy/troop] is at that site, gain [3/1] Power.". First
    /// shipped use of ConditionType.OpponentPresentAtSite/SitePresenceType, chained off
    /// PlaceSpyCommand's own OnSuccess (see PlaceSpyCommand.Execute's SetPendingSiteForChain
    /// call and EffectCondition.EvaluateOpponentPresentAtSite). Runs the TIER 1 test matrix
    /// (planning.txt section 6.D / this task's own delegating prompt) via the REAL "banshee"/
    /// "infiltrator" cards.json entries, mirroring CloakerScenarioTests.cs's style (the closest
    /// sibling - also a PlaceSpy-rooted chained effect) and CraniumRatsScenarioTests.cs's
    /// wrong-player/double-dispatch/stale-target idiom.
    ///
    /// Both cards are unconditional PlaceSpy with a conditionally-gated bonus - PlaceSpy itself
    /// always succeeds if the player has a spy and an eligible site; only the follow-up Power
    /// gain is gated. Every positive/negative test below therefore asserts BOTH halves: the
    /// spy actually landed, AND the Power did (or did not) move.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BansheeInfiltratorScenarioTests
    {
        [TestMethod]
        public void PlayBanshee_OpponentSpyAtTargetSite_PlacesSpyAndGainsThreePower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(blue.Color); // Setup only: Blue already has a spy here.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies, "Red's spy should have been placed regardless of the bonus condition.");
            Assert.AreEqual(3, red.Power, "Another player's spy is present - the +3 Power bonus should have fired.");
        }

        [TestMethod]
        public void PlayBanshee_NoOpponentPresenceAtSite_PlacesSpyButNoPowerGain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            // Deliberately no opponent spy or troop anywhere at this site.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies, "PlaceSpy is unconditional - it must still succeed even though the bonus won't fire.");
            Assert.AreEqual(0, red.Power, "No opponent presence at the site - the bonus must not fire.");
        }

        [TestMethod]
        public void PlayBanshee_OpponentTroopButNoOpponentSpyAtSite_NoPowerGain()
        {
            // Banshee only cares about an opponent SPY - an opponent TROOP alone (Infiltrator's
            // condition) must not leak into Banshee's bonus.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.NodesInternal[0].Occupant = blue.Color; // Setup only: Blue troop, no spy.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(0, red.Power, "An opponent troop (no opponent spy) must not satisfy Banshee's Spy-gated condition.");
        }

        [TestMethod]
        public void PlayInfiltrator_OpponentTroopAtTargetSite_PlacesSpyAndGainsOnePower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.NodesInternal[0].Occupant = blue.Color; // Setup only: Blue already has a troop here.

            var infiltrator = scenario.GiveCard(PlayerColor.Red, "infiltrator");
            scenario.PlayCard(infiltrator);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies, "Red's spy should have been placed regardless of the bonus condition.");
            Assert.AreEqual(1, red.Power, "Another player's troop is present - the +1 Power bonus should have fired.");
        }

        [TestMethod]
        public void PlayInfiltrator_NoOpponentPresenceAtSite_PlacesSpyButNoPowerGain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            // Deliberately no opponent spy or troop anywhere at this site.

            var infiltrator = scenario.GiveCard(PlayerColor.Red, "infiltrator");
            scenario.PlayCard(infiltrator);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies, "PlaceSpy is unconditional - it must still succeed even though the bonus won't fire.");
            Assert.AreEqual(0, red.Power, "No opponent presence at the site - the bonus must not fire.");
        }

        [TestMethod]
        public void PlayInfiltrator_OpponentSpyButNoOpponentTroopAtSite_NoPowerGain()
        {
            // Infiltrator only cares about an opponent TROOP - an opponent SPY alone (Banshee's
            // condition) must not leak into Infiltrator's bonus.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(blue.Color); // Setup only: Blue spy, no troop.

            var infiltrator = scenario.GiveCard(PlayerColor.Red, "infiltrator");
            scenario.PlayCard(infiltrator);

            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(0, red.Power, "An opponent spy (no opponent troop) must not satisfy Infiltrator's Troop-gated condition.");
        }

        [TestMethod]
        public void PlayBanshee_NoSpiesInBarracks_SkipsPlaceSpyEffectCleanly()
        {
            // Pre-existing generic CardRuleEngine.HasValidTargets/PlaceSpyStrategy behavior
            // (planning.txt matrix row 3), not new code from this change - confirmed it still
            // holds for Banshee: with 0 spies, PlaceSpy never even starts targeting, so its
            // OnSuccess (the conditional Power gain) never has a chance to fire either.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            red.SpiesInBarracks = 0;

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(blue.Color); // Condition WOULD be met if PlaceSpy ever started.

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No spies to place - the effect should skip cleanly rather than entering TargetingPlaceSpy.");
            Assert.AreEqual(0, red.Power, "No spy was ever placed, so the conditional bonus must not fire either.");
        }

        [TestMethod]
        public void PlayInfiltrator_NoSpiesInBarracks_SkipsPlaceSpyEffectCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            red.SpiesInBarracks = 0;

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.NodesInternal[0].Occupant = blue.Color; // Condition WOULD be met if PlaceSpy ever started.

            var infiltrator = scenario.GiveCard(PlayerColor.Red, "infiltrator");
            scenario.PlayCard(infiltrator);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No spies to place - the effect should skip cleanly rather than entering TargetingPlaceSpy.");
            Assert.AreEqual(0, red.Power, "No spy was ever placed, so the conditional bonus must not fire either.");
        }

        [TestMethod]
        public void PlayBanshee_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var banshee = scenario.GiveCard(PlayerColor.Blue, "banshee"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(banshee));

            Assert.Contains(banshee, blue.Hand, "Banshee should still be in Blue's hand - the command must not have executed.");
        }

        [TestMethod]
        public void PlaceSpyCommand_TargetingNonexistentSite_IsRejected()
        {
            // Stale/nonexistent target (planning.txt matrix row 5): a forged/corrupted command
            // referencing a site id that doesn't exist in this match.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new PlaceSpyCommand(999999, banshee.Id));
        }

        [TestMethod]
        public void PlaceSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // Double-dispatch/replay (planning.txt matrix row 7): the same PlaceSpyCommand
            // instance re-sent after it already resolved must be a rejected no-op - the site
            // now already has this player's spy, which PlaceSpyCommand.Validate() already
            // guards against (can't stack a second spy of your own on the same site).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var banshee = scenario.GiveCard(PlayerColor.Red, "banshee");
            scenario.PlayCard(banshee);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var command = scenario.Context.ActionSystem.HandleTargetClick(null, site) as PlaceSpyCommand;
            Assert.IsNotNull(command, "Setup check: the click should have produced a real PlaceSpyCommand.");

            scenario.DispatchTwice(command!);

            Assert.HasCount(1, site.Spies.Where(c => c == red.Color), "Red's spy should be at the site exactly once, not twice.");
        }
    }
}
