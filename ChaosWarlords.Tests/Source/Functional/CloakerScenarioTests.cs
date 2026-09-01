using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario coverage for ReturnOwnSpyCommand (see planning.txt TIER 1 - the 2026-09-01
    /// coverage run flagged it at 53.3% line / 66.6% branch). CloakerMechanicsTests.cs already
    /// pins the ActionSystem-internal shape of this flow (hand-typed card, direct
    /// command.Execute(context) calls) - this exercises the same "decline Place a Spy, return
    /// your own spy, assassinate at that site" sequence with the REAL "cloaker" cards.json
    /// entry, through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CloakerScenarioTests
    {
        [TestMethod]
        public void PlayCloaker_DeclineThenReturnOwnSpy_AssassinatesOnlyAtThatSpysSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != siteA);
            var nodeA = siteA.NodesInternal[0];
            var nodeB = siteB.NodesInternal[0];
            nodeA.Occupant = blue.Color;
            nodeB.Occupant = blue.Color;
            siteA.AddSpy(red.Color); // Red already has a spy at Site A - setup only.

            // Assassinate requires Presence at the target node (see MapRuleEngine.CanAssassinate).
            // The spy above grants it WHILE it's still there, but this play returns that exact
            // spy before the Assassinate click happens - give Red a troop adjacent to nodeA too,
            // matching CloakerMechanicsTests.cs's own setup, so presence survives the spy leaving.
            var redPresenceNode = nodeA.Neighbors.First(n => n.Occupant == PlayerColor.None);
            redPresenceNode.Occupant = red.Color;

            var cloaker = scenario.GiveCard(PlayerColor.Red, "cloaker");
            scenario.PlayCard(cloaker);

            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Place a Spy vs. the Alternative.");
            scenario.RespondToLatestInteraction(accept: false); // Decline: use the Alternative.

            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, siteA);

            Assert.DoesNotContain(red.Color, siteA.Spies, "Spy should have left Site A.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Should chain straight into Assassinate.");

            // Wrong site: Site B's troop is NOT where the spy was returned from.
            var rejected = scenario.ClickTarget(nodeB, null);
            Assert.IsNull(rejected, "Assassinating at the wrong site should be rejected by the PendingSite guard.");
            Assert.AreEqual(blue.Color, nodeB.Occupant, "Wrong-site troop must survive.");

            // Correct site: Site A's troop.
            scenario.ClickTarget(nodeA, null);

            Assert.AreEqual(PlayerColor.None, nodeA.Occupant, "Correct-site troop should be assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void ReturnOwnSpyCommand_ForASiteWithNoSpyThere_IsRejectedWithNoStateChange()
        {
            // Adversarial scenario: a forged/stale ReturnOwnSpyCommand naming a site the
            // player has no spy at (already returned, or never placed there) must be rejected
            // by Validate() - not silently pull a spy from somewhere else.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            // Deliberately no spy placed anywhere for Red.

            var cloaker = scenario.GiveCard(PlayerColor.Red, "cloaker");
            scenario.PlayCard(cloaker);
            scenario.RespondToLatestInteraction(accept: false);

            // With no spy anywhere, the Alternative has no valid target - the chain should
            // have already fizzled to Normal (PushEffectNode dead-branch guard), not be
            // sitting in TargetingReturnOwnSpy waiting for an impossible click.
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new ReturnOwnSpyCommand(siteA.Id, cloaker.Id);
            scenario.AssertRejected(forgedCommand);
        }

        [TestMethod]
        public void PlayCloakerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var cloaker = scenario.GiveCard(PlayerColor.Red, "cloaker");

            scenario.DispatchTwice(new PlayCardCommand(cloaker));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }
    }
}
