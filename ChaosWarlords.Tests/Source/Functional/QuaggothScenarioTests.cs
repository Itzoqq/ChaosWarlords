using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Quaggoth ("Assassinate one white troop for each site
    /// you control.") - the first shipped card whose REPEAT COUNT itself (not a resource/draw
    /// amount) is dynamic (CardEffect.DynamicAmountSource, resolved via
    /// CardEffectProcessor.PushEffectContext's new dynamic-repeat-count gate). Unlike Death
    /// Tyrant/Council Member's "up to N" (CardEffect.AllowPartialRepeat), Quaggoth's count is a
    /// fixed, mandatory number computed once at resolution time - no partial decline. Loads the
    /// REAL "quaggoth" entry out of the REAL cards.json and dispatches every command through a
    /// REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class QuaggothScenarioTests
    {
        private static void SetupControllingSites(MatchScenario scenario, PlayerColor color, int siteCount)
        {
            foreach (var site in scenario.Context.MapManager.Sites.Take(siteCount))
            {
                site.Owner = color;
            }
        }

        /// <summary>
        /// Places a white (Neutral) troop at <paramref name="site"/>'s first node and grants Red
        /// Presence there via a spy, so it's a valid Assassinate target.
        /// </summary>
        private static MapNode SetupWhiteTroopWithPresence(Player red, Site site)
        {
            var node = site.NodesInternal[0];
            node.Occupant = PlayerColor.Neutral;
            site.AddSpy(red.Color);
            return node;
        }

        // --- Row 1: happy path - repeat count == sites controlled ---

        [TestMethod]
        public void PlayQuaggoth_Controlling2Sites_AssassinatesExactlyTwoWhiteTroops()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 2);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var nodeA = SetupWhiteTroopWithPresence(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            var nodeB = SetupWhiteTroopWithPresence(red, siteB);

            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            var pendingEffect = scenario.Context.ActionSystem.CurrentSourceEffect!;
            Assert.IsTrue(pendingEffect.TargetNeutralTroopOnly);
            Assert.AreEqual(DynamicAmountSource.SitesControlled, pendingEffect.DynamicAmountSource);

            scenario.ClickTarget(nodeA, null);
            Assert.AreEqual(PlayerColor.None, nodeA.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");

            scenario.ClickTarget(nodeB, null);
            Assert.AreEqual(PlayerColor.None, nodeB.Occupant);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayQuaggoth_NeverTargetsAPlayerColoredTroop()
        {
            // TargetNeutralTroopOnly must be honored even with an enemy troop sitting right
            // there, reachable, at a controlled site.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 1);

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var enemyNode = site.NodesInternal[0];
            enemyNode.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);

            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");
            scenario.PlayCard(card);

            var rejected = scenario.ClickTarget(enemyNode, null);

            Assert.IsNull(rejected, "A player-colored (Blue) troop must be rejected as a target for Quaggoth's neutral-only Assassinate.");
            Assert.AreEqual(PlayerColor.Blue, enemyNode.Occupant);
        }

        // --- Early resolution: fewer valid (reachable, white) targets than sites controlled ---

        [TestMethod]
        public void PlayQuaggoth_Controlling3SitesButOnlyTwoReachableWhiteTroops_ResolvesAfterTwo()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 3);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var nodeA = SetupWhiteTroopWithPresence(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            var nodeB = SetupWhiteTroopWithPresence(red, siteB);
            // Deliberately no 3rd reachable white troop anywhere.

            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");
            scenario.PlayCard(card);

            scenario.ClickTarget(nodeA, null);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");

            scenario.ClickTarget(nodeB, null);

            Assert.AreEqual(2, red.TrophyHall, "Exactly 2 removals - must not wait for or force an impossible 3rd.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3: no-valid-target fallback (via the new 0-controlled-sites gate) ---

        [TestMethod]
        public void PlayQuaggoth_ControllingNoSites_SkipsAssassinateEntirely()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            // Deliberately no site controlled, even though a white troop with presence exists -
            // the dynamic repeat count (0) must skip the whole effect regardless.
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupWhiteTroopWithPresence(red, site);

            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "0 sites controlled must skip Assassinate entirely, not floor to a phantom single repeat.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(0, red.TrophyHall);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayQuaggothCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "quaggoth");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Quaggoth should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, blue.TrophyHall);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileQuaggothEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 1);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupWhiteTroopWithPresence(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Mandatory, not "up to N": DeclineRepeatCommand must be rejected ---

        [TestMethod]
        public void DeclineRepeatCommand_DuringQuaggothsSequence_IsRejected()
        {
            // Quaggoth's card text has no "up to" wording - AllowPartialRepeat must NOT be set,
            // so a player can't voluntarily stop early keeping only some of the dynamic count
            // (unlike Death Tyrant/Council Member).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 2);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupWhiteTroopWithPresence(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            SetupWhiteTroopWithPresence(red, siteB);
            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DeclineRepeatCommand(card.Id), "Quaggoth's Assassinate is mandatory, not 'up to N' - declining early must be rejected.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 2);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var nodeA = SetupWhiteTroopWithPresence(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            var nodeB = SetupWhiteTroopWithPresence(red, siteB);
            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new AssassinateCommand(nodeA.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "nodeA should have been assassinated exactly once, not twice.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed.");
            Assert.AreEqual(PlayerColor.Neutral, nodeB.Occupant, "nodeB must remain untouched by the rejected replay.");

            scenario.ClickTarget(nodeB, null);
            Assert.AreEqual(2, red.TrophyHall);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheFirstTargetThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SetupControllingSites(scenario, PlayerColor.Red, 1);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var node = SetupWhiteTroopWithPresence(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "quaggoth");
            scenario.PlayCard(card);

            var command = new AssassinateCommand(node.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into an AssassinateCommand.");
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, node.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }
    }
}
