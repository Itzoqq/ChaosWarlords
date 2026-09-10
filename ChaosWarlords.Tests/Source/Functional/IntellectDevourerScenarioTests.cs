using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Intellect Devourer ("Choose one: +3 Power. Or, return up
    /// to two troops or spies.") - the first shipped card using EffectType.ReturnUnitOrSpy, a
    /// target-type UNION: a single targeting step (ActionState.TargetingReturnUnitOrSpy) that
    /// accepts EITHER a node click (return that troop - reuses ReturnTroopCommand/
    /// MapManager.CanReturnTroop as-is) OR a site click (return a spy there - the new
    /// ReturnAnySpyCommand/MapManager.CanReturnAnySpy), both already symmetric between the active
    /// player's own unit (no Presence needed) and an enemy's (Presence needed). SupportsRepeat +
    /// AllowPartialRepeat realize "up to two" (Council Member's precedent). Loads the REAL
    /// "intellect_devourer" entry out of the REAL cards.json and dispatches every command through
    /// a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class IntellectDevourerScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with >= 2 empty neighbors, marks 2 of those neighbors
        /// Blue-occupied (troop targets, own/enemy-symmetric ReturnUnit path) - same shape as
        /// DeathbladeScenarioTests/RevenantScenarioTests' own helper.
        /// </summary>
        private static (Player red, MapNode t1, MapNode t2) SetupRedWithTwoAdjacentEnemyTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Count(neighbor => neighbor.Occupant == PlayerColor.None) >= 2);
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));

            var emptyNeighbors = redNode.Neighbors.Where(n => n.Occupant == PlayerColor.None).Take(2).ToList();
            emptyNeighbors[0].Occupant = PlayerColor.Blue;
            emptyNeighbors[1].Occupant = PlayerColor.Blue;

            return (red, emptyNeighbors[0], emptyNeighbors[1]);
        }

        /// <summary>
        /// A site with exactly one enemy (Blue) spy, made returnable via an adjacent Red troop
        /// (not a Red SPY at the site itself - CloakerScenarioTests' precedent) so the site holds
        /// only the one, unambiguous Blue spy.
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


        /// <summary>
        /// Deploys Red at a node with >= 1 empty neighbor and marks it Blue-occupied - a single,
        /// unambiguous troop target (unlike SetupRedWithTwoAdjacentEnemyTroops's 2), used where a
        /// test needs exactly ONE legal target to remain after this repeat resolves.
        /// </summary>
        private static (Player red, MapNode target) SetupRedWithOneAdjacentEnemyTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = PlayerColor.Blue;
            return (red, target);
        }

        // --- Row 1: positive/happy path, accept branch ---

        [TestMethod]
        public void PlayIntellectDevourer_AcceptPower_Gains3PowerAndNeverOpensReturnTargeting()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            int powerBefore = red.Power;
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(powerBefore + 3, red.Power);
            Assert.AreEqual(PlayerColor.Blue, t1.Occupant, "The Return Alternative must never fire when Power is accepted.");
            Assert.AreEqual(PlayerColor.Blue, t2.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 1b/2: positive/happy path, decline branch - both repeats return troops ---

        [TestMethod]
        public void PlayIntellectDevourer_DeclineThenReturnTwoTroops_BothGoBackToTheirOwnersBarracks()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueTroopsBefore = blue.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(t1, null);
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState, "One more repeat still owed.");

            scenario.ClickTarget(t2, null);
            Assert.AreEqual(PlayerColor.None, t2.Occupant);

            Assert.AreEqual(blueTroopsBefore + 2, blue.TroopsInBarracks, "Both returned troops must go back to BLUE's barracks, not Red's.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- The core "target-type union" proof: one troop, one spy, in the same sequence ---

        [TestMethod]
        public void PlayIntellectDevourer_ReturnsOneTroopAndOneSpy_TheUnionWorksAcrossBothRepeats()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, _) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var spySite = SetupCleanEnemySpySite(scenario, red);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueSpiesBefore = blue.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            scenario.ClickTarget(t1, null); // Repeat 1: a troop.
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, spySite); // Repeat 2: a spy.

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(blueSpiesBefore + 1, blue.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Returning the active player's OWN spy - no Presence needed ---

        [TestMethod]
        public void PlayIntellectDevourer_ReturnsOwnSpy_NoPresenceNeededAndReplenishesOwnBarracks()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(red.Color); // Red's own spy, no troop/presence anywhere else.
            int redSpiesBefore = red.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);

            Assert.DoesNotContain(red.Color, site.Spies);
            Assert.AreEqual(redSpiesBefore + 1, red.SpiesInBarracks);
        }

        // --- Voluntary partial decline (AllowPartialRepeat's "up to 2") ---

        [TestMethod]
        public void DeclineRepeatCommand_AfterReturningOneTroop_KeepsItAndStopsThere()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t1, null);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState, "1 more repeat still legally owed.");

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(PlayerColor.None, t1.Occupant, "The already-resolved return must be KEPT.");
            Assert.AreEqual(PlayerColor.Blue, t2.Occupant, "t2 was never touched - declined before being targeted.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayIntellectDevourer_DeclineWithNothingReturnableAnywhere_SkipsTheAlternativeEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayIntellectDevourer_OnlyAnAmbiguousSiteRemains_ResolvesEarlyInsteadOfDemandingAnImpossibleClick()
        {
            // HasValidReturnAnySpyTarget deliberately excludes a site with 2+ simultaneously
            // eligible spies - proves that end-to-end: after the one clean (own-spy) target is
            // used, the only site left is ambiguous (Red's own + Blue's, both eligible), so the
            // repeat must resolve early rather than opening targeting with no legal way through.
            // Uses own-spy targets specifically (not troops) so NOTHING is left deployed anywhere
            // that MapRuleEngine.HasValidReturnTroopTarget's own "you may return your own troop
            // too" rule (see its doc comment) would otherwise offer as a permanent fallback.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var cleanSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            cleanSite.AddSpy(red.Color);
            var ambiguousSite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != cleanSite);
            ambiguousSite.AddSpy(red.Color);
            ambiguousSite.AddSpy(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(null, cleanSite); // Only 1 of the 2 requested repeats has any legal target.

            Assert.DoesNotContain(red.Color, cleanSite.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No completable target remains - must resolve instead of waiting forever.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.Contains(red.Color, ambiguousSite.Spies, "The ambiguous site's spies must survive untouched.");
            Assert.Contains(PlayerColor.Blue, ambiguousSite.Spies);
        }

        // --- The known, documented ambiguous-site click-resolution gap ---

        [TestMethod]
        public void ClickingAnAmbiguousSite_IsRejectedByTheInputLayer_ButAForgedCommandNamingOneColorStillWorks()
        {
            var scenario = MatchScenario.Build();
            var (red, _) = SetupRedWithOneAdjacentEnemyTroop(scenario); // Keeps HasValidTargets true via the troop path even though the spy site below is ambiguous.
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(red.Color);
            site.AddSpy(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            var rejected = scenario.ClickTarget(null, site);
            Assert.IsNull(rejected, "An ambiguous site click must be rejected by the input layer, not guess which spy was meant.");
            Assert.Contains(red.Color, site.Spies, "Nothing should have moved.");
            Assert.Contains(PlayerColor.Blue, site.Spies);

            // The command-level primitive itself is fully generic - a client naming ONE specific
            // color explicitly still works correctly regardless of how many OTHER candidates were
            // at that site (only the click-to-command input-layer convenience has the gap).
            var forgedCommand = new ReturnAnySpyCommand(site.Id, PlayerColor.Blue, card.Id);
            scenario.Dispatch(forgedCommand);

            Assert.Contains(red.Color, site.Spies, "Red's own spy must be untouched.");
            Assert.DoesNotContain(PlayerColor.Blue, site.Spies);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayIntellectDevourerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "intellect_devourer");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Intellect Devourer should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent/already-moved target for each sub-type ---

        [TestMethod]
        public void ReturnTroopCommand_TargetingTheAlreadyReturnedNode_IsRejectedForTheSecondRepeat()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, _) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t1, null);

            var forgedCommand = new ReturnTroopCommand(t1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-returned (now empty) node must be rejected as a target for the 2nd repeat.");
        }

        [TestMethod]
        public void ReturnTroopCommand_TargetingANonexistentNode_IsRejectedWhileIntellectDevourerEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var forgedCommand = new ReturnTroopCommand(999999, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void ReturnAnySpyCommand_TargetingANonexistentSite_IsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var forgedCommand = new ReturnAnySpyCommand(999999, PlayerColor.Blue, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent site id must be rejected.");
        }

        [TestMethod]
        public void ReturnAnySpyCommand_TargetingAnEnemySpyWithoutPresence_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(PlayerColor.Blue); // No Red presence anywhere near this site.
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var forgedCommand = new ReturnAnySpyCommand(site.Id, PlayerColor.Blue, card.Id);
            scenario.AssertRejected(forgedCommand, "Returning an enemy spy without Presence must be rejected.");
            Assert.Contains(PlayerColor.Blue, site.Spies);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void ReturnTroopCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.DispatchTwice(new ReturnTroopCommand(t1.Id, card.Id));

            Assert.AreEqual(PlayerColor.None, t1.Occupant, "t1 should have been returned exactly once.");
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed.");
            Assert.AreEqual(PlayerColor.Blue, t2.Occupant, "t2 must remain untouched by the rejected replay.");

            scenario.ClickTarget(t2, null);
            Assert.AreEqual(PlayerColor.None, t2.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void ReturnAnySpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = SetupCleanEnemySpySite(scenario, red);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueSpiesBefore = blue.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.DispatchTwice(new ReturnAnySpyCommand(spySite.Id, PlayerColor.Blue, card.Id));

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(blueSpiesBefore + 1, blue.SpiesInBarracks, "The barracks credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip for BOTH command types this card can produce ---

        [TestMethod]
        public void ReturnTroopCommand_DtoRoundTrip_StillReturnsTheFirstTargetThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, _) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var command = new ReturnTroopCommand(t1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as ReturnTroopCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, t1.Occupant);
        }

        [TestMethod]
        public void ReturnAnySpyCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = SetupCleanEnemySpySite(scenario, red);
            var card = scenario.GiveCard(PlayerColor.Red, "intellect_devourer");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var command = new ReturnAnySpyCommand(spySite.Id, PlayerColor.Blue, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as ReturnAnySpyCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);
            Assert.AreEqual(command.SpyColor, hydrated.SpyColor);

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
        }
    }
}
