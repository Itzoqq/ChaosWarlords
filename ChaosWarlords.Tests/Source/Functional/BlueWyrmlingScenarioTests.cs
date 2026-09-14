using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Blue Wyrmling ("Gain 3 Influence. Return another
    /// player's troop or spy.") - planning.txt TIER 1 item 8. Two independent mandatory effects:
    /// an automatic GainResource(Influence,3) followed by a mandatory ReturnUnitOrSpy
    /// (ReturnEnemyOnly), the same enemy-only Return shape High Priest of Myrkul's first half
    /// already exercises. Loads the REAL "blue_wyrmling" entry out of the REAL cards.json and
    /// dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BlueWyrmlingScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithOneAdjacentEnemyTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = PlayerColor.Blue;
            return (red, target);
        }

        /// <summary>
        /// A site with exactly one enemy (Blue) spy, made returnable via an adjacent Red troop -
        /// returning an ENEMY spy needs Presence at that site (tyrants-rules skill section 5),
        /// unlike returning your own - same shape as HighPriestOfMyrkulScenarioTests.
        /// SetupCleanEnemySpySite.
        /// </summary>
        private static Site SetupCleanEnemySpySite(MatchScenario scenario, Player red)
        {
            foreach (var site in scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0))
            {
                foreach (var siteNode in site.NodesInternal)
                {
                    var redPresenceNode = siteNode.Neighbors.FirstOrDefault(n => n.Occupant == PlayerColor.None);
                    if (redPresenceNode != null)
                    {
                        site.AddSpy(PlayerColor.Blue);
                        redPresenceNode.Occupant = red.Color;
                        return site;
                    }
                }
            }

            throw new InvalidOperationException("No site found with an available adjacent node for Presence.");
        }

        // --- Row 1: positive/happy path, Return via a troop target ---

        [TestMethod]
        public void PlayBlueWyrmling_GrantsInfluenceAndReturnsAdjacentEnemyTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueTroopsBefore = blue.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "blue_wyrmling");

            scenario.PlayCard(card);
            Assert.AreEqual(3, red.Influence, "The mandatory GainResource must apply regardless of the Return targeting outcome.");
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(blueTroopsBefore + 1, blue.TroopsInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 1b: positive/happy path, Return via a spy target ---

        [TestMethod]
        public void PlayBlueWyrmling_ReturnsEnemySpyViaSiteClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = SetupCleanEnemySpySite(scenario, red);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueSpiesBefore = blue.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "blue_wyrmling");

            scenario.PlayCard(card);
            scenario.ClickTarget(null, site);

            Assert.DoesNotContain(PlayerColor.Blue, site.Spies);
            Assert.AreEqual(blueSpiesBefore + 1, blue.SpiesInBarracks);
        }

        // --- Row 3: no-valid-target fallback - own troop only, not a valid enemy-only Return target ---

        [TestMethod]
        public void PlayBlueWyrmling_OnlyOwnTroopOnBoard_ReturnSkipsEntirely_NoSelfTargetOffered()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var card = scenario.GiveCard(PlayerColor.Red, "blue_wyrmling");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.Influence, "The independent GainResource effect must still resolve.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "A lone own troop is not a valid target for enemy-only Return.");
            Assert.AreEqual(red.Color, redNode.Occupant, "Red's own troop must be untouched.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayBlueWyrmlingCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "blue_wyrmling");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.AreEqual(0, blue.Influence);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void ReturnUnitCommand_TargetingANonexistentNode_IsRejectedWhileBlueWyrmlingEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "blue_wyrmling");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new ReturnTroopCommand(999999, card.Id), "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayBlueWyrmlingCommand_DispatchedTwice_SecondDispatchIsRejectedAndDoesNotDoubleGrant()
        {
            var scenario = MatchScenario.Build();
            var (red, _) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "blue_wyrmling");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(3, red.Influence, "Should have applied exactly once, not twice.");
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState, "Should still be waiting for exactly the one click the first play triggered.");
        }
    }
}
