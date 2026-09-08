using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Green Dragon ("Choose one: Place a spy, then supplant a
    /// troop at that spy's site. Or, return one of your spies, then supplant a troop at that
    /// spy's site, then gain 1 VP for each site you control.") - pure DATA, zero new engine work:
    /// branch A reuses the already-shipped PlaceSpy -> OnSuccess:Supplant site-scoped chain
    /// (Banshee/Infiltrator already chain an OnSuccess directly off PlaceSpy;
    /// PlaceSpyCommand.Execute already calls ActionSystem.SetPendingSiteForChain on every
    /// success), branch B reuses Graz'zt's exact ReturnOwnSpy -> OnSuccess:Supplant shape (single
    /// round, not Graz'zt's ChainedRepeatCount "any number"), and the trailing VP line reuses
    /// White Dragon's DynamicAmountSource.SitesControlled (a "for each," not White Dragon's "for
    /// every 2" - DynamicAmountDivisor simply omitted, defaulting to 1). Confirms planning.txt's
    /// "site control MARKER" blocker was stale: Site.Owner (freshly recalculated by
    /// SiteControlSystem, never a live-recomputed snapshot only at query time) already IS the
    /// site control marker the rulebook describes. Loads the REAL "green_dragon" entry out of the
    /// REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class GreenDragonScenarioTests
    {
        /// <summary>
        /// Places a Red spy at <paramref name="site"/> and a Blue troop at one of its nodes, plus
        /// a separate Red troop adjacent to that node so Presence survives the spy leaving - same
        /// setup GrazztScenarioTests.cs uses for the identical ReturnOwnSpy -> Supplant chain.
        /// </summary>
        private static MapNode SetupSpySiteWithSupplantableTroop(Player red, Site site)
        {
            var troopNode = site.NodesInternal[0];
            troopNode.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);
            var presenceNode = troopNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            presenceNode.Occupant = red.Color;
            return troopNode;
        }

        /// <summary>
        /// Marks the first <paramref name="siteCount"/> sites on the board as owned by Red -
        /// setup only, not through a command (same pattern WhiteDragonScenarioTests.cs uses for
        /// DynamicAmountSource.SitesControlled).
        /// </summary>
        private static void SetupRedControllingSites(MatchScenario scenario, PlayerColor color, int siteCount)
        {
            foreach (var site in scenario.Context.MapManager.Sites.Take(siteCount))
            {
                site.Owner = color;
            }
        }

        // --- Row 1: happy path, accept branch A (Place a spy, then supplant at that site) ---

        [TestMethod]
        public void PlayGreenDragon_AcceptPlaceSpy_PlacesSpyThenSupplantsAtThatSiteWithNoVpGranted()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troop = site.NodesInternal[0];
            troop.Occupant = PlayerColor.Blue;
            int vpBefore = red.VictoryPoints;
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Chains straight into Supplant, scoped to the just-spied site.");
            Assert.Contains(red.Color, site.Spies, "Placing the spy (branch A) never returns it - unlike branch B.");

            scenario.ClickTarget(troop, null);

            Assert.AreEqual(red.Color, troop.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "The assassinated half of Supplant sends Blue's troop to Red's trophy hall.");
            Assert.AreEqual(vpBefore, red.VictoryPoints, "Branch A's card text has no VP clause at all.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayGreenDragon_AcceptPlaceSpyAtASiteWithNoTroopWhileTheBoardHasOneElsewhere_ResolvesCleanlyInsteadOfStalling()
        {
            // Regression test for a real, reproducible soft-lock this card's own review found:
            // SupplantStrategy.HasValidTargets used to check the WHOLE board for a valid
            // Assassinate-half target, ignoring ActionSystem.PendingSite - so accepting "place a
            // spy?" at an empty/uncontested site (a completely ordinary choice) would still
            // chain into TargetingSupplant, because SOME OTHER site had an enemy troop. From
            // there every click - the real target elsewhere, or the empty spied site - was
            // rejected by SupplantCommand.Validate()'s own site-scoping check, with nothing to
            // auto-resolve the effect as a clean "no valid target" instead. Fixed by making
            // SupplantStrategy.HasValidTargets always honor PendingSite, matching Validate()'s
            // own unconditional behavior - so this must now resolve cleanly to Normal.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var emptySite = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var siteWithATroop = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0 && s != emptySite);
            var enemyTroopNode = siteWithATroop.NodesInternal[0];
            enemyTroopNode.Occupant = blue.Color; // A globally-valid Supplant target, but at the WRONG site.
            // Red needs genuine Presence here too, or this wouldn't be a globally-valid target
            // in the first place regardless of the site-scoping bug under test.
            enemyTroopNode.Neighbors.First(n => n.Occupant == PlayerColor.None).Occupant = red.Color;
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true); // Accept branch A: place a spy.
            scenario.ClickTarget(null, emptySite); // Placed at the site with NO troop to supplant.

            Assert.Contains(red.Color, emptySite.Spies, "The spy should still have been placed.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "emptySite has no troop to Supplant - the chain must resolve cleanly, not stall in TargetingSupplant.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(blue.Color, siteWithATroop.NodesInternal[0].Occupant, "siteWithATroop's troop was never a legal target for this chain - must survive untouched.");
        }

        // --- Row 1b: happy path, decline branch A -> branch B, dynamic VP amount ---

        [TestMethod]
        public void PlayGreenDragon_DeclineThenReturnSpyAndSupplant_GrantsOneVpPerSiteControlled()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            SetupRedControllingSites(scenario, PlayerColor.Red, 3);
            int redSpiesBefore = red.SpiesInBarracks;
            int vpBefore = red.VictoryPoints;
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline branch A (Place a spy).
            // Unlike Graz'zt's inner ReturnOwnSpy (itself IsOptional, for its "any number,
            // including zero" ChainedRepeatCount rounds), Green Dragon's branch B is a single,
            // mandatory step once branch A is declined - no second confirmation popup, straight
            // into targeting.
            Assert.HasCount(1, scenario.Interactions, "Declining branch A must not raise a second confirmation popup - branch B is mandatory once reached.");
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(null, site);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Chains straight into Supplant, scoped to this site.");
            scenario.ClickTarget(troop, null);

            Assert.AreEqual(red.Color, troop.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(redSpiesBefore + 1, red.SpiesInBarracks, "Branch B returns the spy to the barracks - unlike branch A.");
            Assert.DoesNotContain(red.Color, site.Spies);
            Assert.AreEqual(vpBefore + 3, red.VictoryPoints, "1 VP for each of the 3 sites Red controls - a plain 'for each,' not 'for every N' (no divisor).");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayGreenDragon_DeclineThenReturnSpyAndSupplant_StillTiedAfterSupplantGrantsZeroVp()
        {
            // A second Blue troop at the same site survives the Supplant, keeping Red's new
            // 1-troop presence tied with Blue's remaining 1 (ties = no one controls it,
            // tyrants-rules.pdf p.10) - unlike the "3 sites controlled" test above, this
            // confirms the VP amount is 0 rather than incidentally counting the very site this
            // Supplant just (would-be) fought over.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var troop = site.NodesInternal[0];
            troop.Occupant = PlayerColor.Blue; // Will be supplanted -> becomes Red's only in-site troop.
            site.NodesInternal[1].Occupant = PlayerColor.Blue; // Survives - keeps the site tied, not Red-won.
            site.AddSpy(red.Color);
            // Presence via a node OUTSIDE this site entirely (a route node, not one of
            // site.NodesInternal) - unlike GrazztScenarioTests'/the shared helper's own
            // same-site neighbor pick, this must NOT add a second in-site Red troop, or the tie
            // this test depends on would break.
            var externalPresenceNode = troop.Neighbors.First(n => n.Occupant == PlayerColor.None && !site.NodesInternal.Contains(n));
            externalPresenceNode.Occupant = red.Color;
            int vpBefore = red.VictoryPoints;
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline branch A - straight into branch B's mandatory ReturnOwnSpy targeting, no second popup.
            scenario.ClickTarget(null, site);
            scenario.ClickTarget(troop, null);

            Assert.AreEqual(PlayerColor.None, site.Owner, "Setup check: Red's new troop must tie, not win, control of this site.");
            Assert.AreEqual(vpBefore, red.VictoryPoints, "Controlling 0 sites must grant 0 VP, not throw or fall back to a default amount.");
        }

        // --- Row 3: no-valid-target fallback for BOTH branches ---

        [TestMethod]
        public void PlayGreenDragon_NothingToPlaceOrReturn_DoesNothingAtAllCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SpiesInBarracks = 0; // No barracks stock - Place a Spy has no valid target.
            // Deliberately no spy placed anywhere - Return has no valid target either.
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "Neither branch has a valid target - no popup should ever be raised.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayGreenDragonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "green_dragon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Green Dragon should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, Supplant site-scoping defense ---

        [TestMethod]
        public void SupplantCommand_TargetingTheWrongSite_IsRejectedByValidateDefenseInDepth()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var siteA = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troopA = SetupSpySiteWithSupplantableTroop(red, siteA);
            var siteB = scenario.Context.MapManager.Sites.First(s => s.Name == "Shadow Market");
            var troopB = SetupSpySiteWithSupplantableTroop(red, siteB);
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline branch A - straight into branch B's mandatory ReturnOwnSpy targeting, no second popup.
            scenario.ClickTarget(null, siteA); // Returns from Site A - PendingSite now Site A.

            var forgedCommand = new SupplantCommand(troopB.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "Supplanting at Site B's troop while PendingSite is Site A must be rejected.");
            Assert.AreEqual(PlayerColor.Blue, troopB.Occupant);

            scenario.ClickTarget(troopA, null); // The correct site still works.
            Assert.AreEqual(red.Color, troopA.Occupant);
        }

        [TestMethod]
        public void ReturnOwnSpyCommand_ForASiteWithNoSpyThere_IsRejectedDuringGreenDragonsChain()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            SetupSpySiteWithSupplantableTroop(red, site);
            var untouchedSite = scenario.Context.MapManager.Sites.First(s => s.Name == "Shadow Market");
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline branch A - straight into branch B's mandatory ReturnOwnSpy targeting, no second popup.

            var forgedCommand = new ReturnOwnSpyCommand(untouchedSite.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A site with no Red spy must be rejected as a return target.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayGreenDragonCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceDuringBranchA_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troop = site.NodesInternal[0];
            troop.Occupant = PlayerColor.Blue;
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, site);
            scenario.DispatchTwice(new SupplantCommand(troop.Id, card.Id));

            Assert.AreEqual(red.Color, troop.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "The trophy-hall credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void PlaceSpyCommand_DtoRoundTrip_StillPlacesTheSpyAndChainsIntoSupplant()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.NodesInternal[0].Occupant = PlayerColor.Blue;
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            var command = new PlaceSpyCommand(site.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as PlaceSpyCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);

            scenario.Dispatch(hydrated);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "The chained Supplant must still open through a hydrated dispatch.");
        }

        [TestMethod]
        public void SupplantCommand_DtoRoundTrip_StillSupplantsThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troop = SetupSpySiteWithSupplantableTroop(red, site);
            var card = scenario.GiveCard(PlayerColor.Red, "green_dragon");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Decline branch A - straight into branch B's mandatory ReturnOwnSpy targeting, no second popup.
            scenario.ClickTarget(null, site);

            var command = new SupplantCommand(troop.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as SupplantCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, troop.Occupant);
        }
    }
}
