using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Red Dragon ("Supplant a troop. Return an enemy spy.
    /// Gain 1 VP for each site under your total control.") - 3 independent top-level Effects
    /// list entries (not chained via OnSuccess), so all 3 are pushed onto ActionSystem's
    /// execution stack up front when the card is played (CardEffectProcessor.ResolveEffects'
    /// reverse-order push), then resolved one at a time in listed order. The trailing VP line's
    /// dynamic amount is only actually COMPUTED once it's popped off the stack (last), which -
    /// since Supplant/ReturnEnemySpy both resolve first - is naturally AFTER whatever site-
    /// control change the Supplant just caused, matching the card's sequential reading. The
    /// Return-an-enemy-spy half needed a genuinely new EffectType.ReturnEnemySpy (reusing the
    /// existing paid-basic-action machinery - ActionState.TargetingReturnSpy,
    /// SpySubsystem.HandleReturnSpyInitialClick, ResolveSpyCommand - which already waives its
    /// Power cost whenever CardId is set), plus a new DynamicAmountSource.SitesUnderTotalControl
    /// case. Loads the REAL "red_dragon" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class RedDragonScenarioTests
    {
        /// <summary>
        /// An enemy (Blue) troop at <paramref name="site"/>'s first node (every other node left
        /// empty, so supplanting it gives Red sole control), and Red Presence via Red's OWN spy
        /// AT that same site - deliberately not an adjacent troop: this test uses MULTIPLE
        /// sites at once and checks their exact control/total-control state, so a helper that
        /// places a troop on "the first empty neighbor node" without excluding the target
        /// site's OWN other nodes (sites can have internal node-to-node adjacency - see the
        /// generic Cloaker/Graz'zt helpers this pattern is normally borrowed from, which DO
        /// exclude the target site's own nodes) risks silently landing the presence troop on a
        /// sibling node of the SAME site instead, corrupting the exact count this test verifies.
        /// A spy at the target site itself grants Presence there with no such risk.
        /// </summary>
        private static MapNode SetupSupplantableSite(Player red, Site site)
        {
            var node = site.NodesInternal[0];
            node.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);
            return node;
        }

        /// <summary>
        /// A site with a single enemy (Blue) spy, and Red Presence via Red's OWN spy at that same
        /// site (see SetupSupplantableSite's own doc comment for why not an adjacent troop).
        /// </summary>
        private static void SetupSingleEnemySpySite(Player red, Site site)
        {
            site.AddSpy(PlayerColor.Blue);
            site.AddSpy(red.Color);
        }

        // --- Row 1: happy path, plus the ordering-sensitive VP amount ---

        [TestMethod]
        public void PlayRedDragon_SupplantGrantingTotalControl_CountsItInTheTrailingVp()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int vpBefore = red.VictoryPoints;

            var supplantSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var enemyNode = SetupSupplantableSite(red, supplantSite);

            var spySite = scenario.Context.MapManager.Sites.First(s => s != supplantSite && s.NodesInternal.Count > 0);
            SetupSingleEnemySpySite(red, spySite);

            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(PlayerColor.None, supplantSite.Owner, "Setup check: not yet controlled by Red before the Supplant.");

            scenario.ClickTarget(enemyNode, null);

            Assert.AreEqual(red.Color, enemyNode.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(red.Color, supplantSite.Owner, "Supplanting the site's only node should immediately grant Red control.");
            Assert.IsTrue(supplantSite.HasTotalControl, "No enemy troops or spies remain - this should be Red's TOTAL control too.");
            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState, "Chains to the next independent top-level effect.");

            scenario.ClickTarget(null, spySite);

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            // If the VP amount had been (wrongly) computed BEFORE the Supplant, this would be 0.
            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "1 VP for the 1 site now under Red's total control - counted AFTER the Supplant that caused it.");
        }

        // --- Multi-enemy-spy disambiguation (base action's own sub-flow, reused here) ---

        [TestMethod]
        public void PlayRedDragon_ReturnEnemySpyAtASiteWithTwoEnemySpies_RequiresColorDisambiguation()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var supplantSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var enemyNode = SetupSupplantableSite(red, supplantSite);

            var spySite = scenario.Context.MapManager.Sites.First(s => s != supplantSite && s.NodesInternal.Count > 0);
            spySite.AddSpy(PlayerColor.Blue);
            spySite.AddSpy(PlayerColor.Black);
            spySite.AddSpy(red.Color); // Presence.

            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");
            scenario.PlayCard(card);
            scenario.ClickTarget(enemyNode, null);

            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState);
            var ambiguousClick = scenario.ClickTarget(null, spySite);
            Assert.IsNull(ambiguousClick, "A site click alone can't resolve which of 2 enemy spies to return.");
            Assert.AreEqual(ActionState.SelectingSpyToReturn, scenario.Context.ActionSystem.CurrentState);

            scenario.SelectSpyColorToReturn(PlayerColor.Black);

            Assert.DoesNotContain(PlayerColor.Black, spySite.Spies);
            Assert.Contains(PlayerColor.Blue, spySite.Spies, "Only the selected color should have been returned.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 3: no-valid-target fallback for the Supplant half specifically ---

        [TestMethod]
        public void PlayRedDragon_NoSupplantTargetButAnEnemySpyExists_SkipsSupplantButStillReturnsTheSpy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            // Deliberately no Supplant target anywhere.
            var spySite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSingleEnemySpySite(red, spySite);

            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");
            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState, "Supplant's own independent push should have skipped straight past with no valid target.");
            Assert.AreEqual(0, red.TrophyHall);

            scenario.ClickTarget(null, spySite);

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayRedDragon_NothingToSupplantOrReturn_StillGrantsZeroVpCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int vpBefore = red.VictoryPoints;
            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(vpBefore, red.VictoryPoints, "0 sites under total control must grant 0 VP.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayRedDragonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "red_dragon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Red Dragon should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, adversarial ResolveSpyCommand forgery ---

        [TestMethod]
        public void ResolveSpyCommand_NamingTheActivePlayersOwnColor_IsRejectedDuringRedDragonsChain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var supplantSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var enemyNode = SetupSupplantableSite(red, supplantSite);
            var spySite = scenario.Context.MapManager.Sites.First(s => s != supplantSite && s.NodesInternal.Count > 0);
            SetupSingleEnemySpySite(red, spySite); // Also places Red's own spy there, for Presence.

            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");
            scenario.PlayCard(card);
            scenario.ClickTarget(enemyNode, null);
            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new ResolveSpyCommand(spySite.Id, red.Color, card.Id);
            scenario.AssertRejected(forgedCommand, "A forged command naming Red's OWN spy color must be rejected.");
            Assert.Contains(red.Color, spySite.Spies);
        }

        [TestMethod]
        public void SupplantCommand_TargetingANonexistentNode_IsRejectedWhileRedDragonEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var supplantSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSupplantableSite(red, supplantSite);
            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new SupplantCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void SupplantCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var supplantSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var enemyNode = SetupSupplantableSite(red, supplantSite);
            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new SupplantCommand(enemyNode.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The trophy-hall credit must not have been applied twice.");
            Assert.AreEqual(red.Color, enemyNode.Occupant);
        }

        [TestMethod]
        public void ResolveSpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSingleEnemySpySite(red, spySite);
            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState, "Setup check: no Supplant target, straight to ReturnEnemySpy.");
            scenario.DispatchTwice(new ResolveSpyCommand(spySite.Id, PlayerColor.Blue, card.Id));

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void SupplantCommand_DtoRoundTrip_StillSupplantsThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var supplantSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var enemyNode = SetupSupplantableSite(red, supplantSite);
            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");
            scenario.PlayCard(card);

            var command = new SupplantCommand(enemyNode.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as SupplantCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, enemyNode.Occupant);
        }

        [TestMethod]
        public void ResolveSpyCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            SetupSingleEnemySpySite(red, spySite);
            var card = scenario.GiveCard(PlayerColor.Red, "red_dragon");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState, "Setup check: no Supplant target, straight to ReturnEnemySpy.");

            var command = new ResolveSpyCommand(spySite.Id, PlayerColor.Blue, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as ResolveSpyCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.SiteId, hydrated!.SiteId);

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
        }
    }
}
