using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for High Priest of Myrkul ("Return another player's troop
    /// or spy. At end of turn, you may promote ANY NUMBER of Undead cards played this turn.") -
    /// closes planning.txt TIER 1 item 7's last open piece.
    ///
    /// Exercises 3 new primitives together, all first shipped by this card:
    /// - Card.CreatureType/CardCreatureType.Undead - a new per-card data dimension (top-right
    ///   printed label), independent of CardAspect.
    /// - CardEffect.PromoteAnyNumber/RequiredPromotionCreatureType - TurnContext.
    ///   AddUnboundedPromotionCredit banks ONE optional, creature-type-filtered credit per
    ///   currently-eligible played card, materialized lazily (TurnContext.
    ///   ExpandPendingUnboundedCredits) the first time this turn's credits are actually read -
    ///   see that method's own doc comment for why that's what makes "any number... played this
    ///   turn" correct even for Undead cards played AFTER this one resolves.
    /// - CardEffect.ReturnEnemyOnly - restricts EffectType.ReturnUnitOrSpy's target-type union
    ///   (Intellect Devourer's own-or-enemy shape) to enemy-only, filling the gap between that
    ///   effect (too broad) and EffectType.ReturnEnemySpy (enemy-only but spy-only).
    ///
    /// Loads the REAL "high_priest_of_myrkul" entry out of the REAL cards.json and dispatches
    /// every command through a REAL CommandDispatcher, mirroring IntellectDevourerScenarioTests.
    /// cs's own setup patterns for the Return half.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class HighPriestOfMyrkulScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with >= 1 empty neighbor and marks it Blue-occupied - a single,
        /// unambiguous enemy troop target.
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

        /// <summary>
        /// A site with exactly one enemy (Blue) spy, made returnable via an adjacent Red troop -
        /// same shape as IntellectDevourerScenarioTests.SetupCleanEnemySpySite.
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
        /// Plays "skeletal_horde" (a shipped Undead card) and declines its own optional
        /// Devour-self choice, so it stays in Red's Played pile - exactly the state a real
        /// "Undead card played this turn" needs to be in for High Priest's unbounded credit to
        /// count it.
        /// </summary>
        private static void PlayAndKeepAnUndeadCard(MatchScenario scenario, PlayerColor color)
        {
            var undeadCard = scenario.GiveCard(color, "skeletal_horde");
            scenario.PlayCard(undeadCard);
            scenario.RespondToLatestInteraction(accept: false);
        }

        // --- Row 1: positive/happy path - Return via a troop target. ---

        [TestMethod]
        public void PlayHighPriestOfMyrkul_ReturnsAdjacentEnemyTroop_ToItsOwnersBarracks()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueTroopsBefore = blue.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(blueTroopsBefore + 1, blue.TroopsInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 1b: positive/happy path - Return via a spy target. ---

        [TestMethod]
        public void PlayHighPriestOfMyrkul_ReturnsEnemySpy_ViaSiteClick()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = SetupCleanEnemySpySite(scenario, red);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueSpiesBefore = blue.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            scenario.ClickTarget(null, spySite);

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(blueSpiesBefore + 1, blue.SpiesInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- The core new restriction: enemy-only, unlike Intellect Devourer's own-or-enemy. ---

        [TestMethod]
        public void PlayHighPriestOfMyrkul_OnlyOwnTroopOnBoard_ReturnSkipsEntirely_NoSelfTargetOffered()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);

            // Unlike Intellect Devourer (own-or-enemy), a lone own troop is NOT a valid target
            // for High Priest's enemy-only Return - the mandatory effect must resolve as a clean
            // no-op instead of opening a targeting state with nothing legally clickable.
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(red.Color, redNode.Occupant, "Red's own troop must be untouched.");
        }

        [TestMethod]
        public void ReturnTroopCommand_ForgedTargetingOwnTroop_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            // Give Red an enemy troop target too, so the effect actually opens targeting (a
            // pure "nothing valid at all" case would resolve early per the test above instead).
            var (_, enemyTarget) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var redNode = scenario.Context.MapManager.Nodes.First(n => n.Occupant == red.Color);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new ReturnTroopCommand(redNode.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "Enemy-only must reject a forged attempt to return the active player's own troop.");

            Assert.AreEqual(red.Color, redNode.Occupant, "Red's own troop must still be there.");
            Assert.AreEqual(PlayerColor.Blue, enemyTarget.Occupant, "Untouched.");
        }

        [TestMethod]
        public void ReturnAnySpyCommand_ForgedNamingOwnSpyColor_IsRejected()
        {
            var scenario = MatchScenario.Build();
            // Deploy Red's initial troop FIRST, while Red still has zero board presence (the
            // Setup-phase "first unit, deploy anywhere" freebie - MapRuleEngine.
            // CanDeployDuringSetup requires PlayerHasPresenceOnMap to still be false, and a spy
            // already placed would count as presence and break that).
            var (red, _) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(red.Color);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new ReturnAnySpyCommand(site.Id, red.Color, card.Id);
            scenario.AssertRejected(forgedCommand, "Enemy-only must reject a forged attempt to return the active player's own spy.");

            Assert.Contains(red.Color, site.Spies, "Red's own spy must still be there.");
        }

        // --- The core new mechanic: "promote any number of Undead cards played this turn". ---

        [TestMethod]
        public void PlayHighPriestOfMyrkul_ThenTwoUndeadCardsPlayedThisTurn_BanksOneCreditPerUndeadCard()
        {
            // Deliberately does NOT read PendingPromotionsCount (or any other credit-reading
            // method) between playing High Priest and playing the 2 Undead cards below - doing
            // so would itself trigger TurnContext.ExpandPendingUnboundedCredits early, freezing
            // the credit count at whatever was played so far, exactly the premature-expansion
            // hazard the "any number... played this turn" wording must NOT be vulnerable to.
            // Production code only ever reads these once redemption genuinely starts (end of
            // turn) - this test mirrors that same discipline instead of polling mid-turn.
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");
            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            PlayAndKeepAnUndeadCard(scenario, PlayerColor.Red);
            PlayAndKeepAnUndeadCard(scenario, PlayerColor.Red);

            // NOW (only now that the turn's played-card set is final) it's safe to force
            // expansion and read the count - PendingPromotionsCount deliberately does not
            // self-expand (see its own doc comment); ForfeitUnsatisfiableCredits is the real,
            // safe trigger a genuine redemption session always calls first.
            var turnContext = scenario.Context.TurnManager.CurrentTurnContext;
            turnContext.ForfeitUnsatisfiableCredits(red.PlayedCards);
            Assert.AreEqual(2, turnContext.PendingPromotionsCount, "One credit per Undead card played this turn, even though both were played AFTER High Priest resolved.");
        }

        [TestMethod]
        public void PlayHighPriestOfMyrkul_NoUndeadCardsPlayedThisTurn_BanksNoPromotionCredits()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayHighPriestOfMyrkul_CreditsAreFilteredToUndead_ANonUndeadCardIsNotAValidTarget()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");
            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            PlayAndKeepAnUndeadCard(scenario, PlayerColor.Red);
            var plainCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.PlayCard(plainCard);

            Assert.IsFalse(scenario.Context.TurnManager.CurrentTurnContext.HasValidCreditFor(plainCard), "core_house_guard has no CreatureType at all - must not be a valid target for an Undead-filtered credit.");
        }

        [TestMethod]
        public void PlayHighPriestOfMyrkul_PromoteCreditsAreOptional_CanEndTurnWithoutRedeemingAny()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");
            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            PlayAndKeepAnUndeadCard(scenario, PlayerColor.Red);
            var turnContext = scenario.Context.TurnManager.CurrentTurnContext;
            turnContext.ForfeitUnsatisfiableCredits(red.PlayedCards);
            Assert.AreEqual(1, turnContext.PendingPromotionsCount, "Setup check.");

            Assert.IsTrue(turnContext.CanDeclineRemainingPromotions, "\"Any number, including zero\" can never be mandatory.");
        }

        [TestMethod]
        public void PlayHighPriestOfMyrkul_PromotingTheEligibleUndeadCard_MovesItToInnerCircle()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");
            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            var undeadCard = scenario.GiveCard(PlayerColor.Red, "skeletal_horde");
            scenario.PlayCard(undeadCard);
            scenario.RespondToLatestInteraction(accept: false);

            var turnContext = scenario.Context.TurnManager.CurrentTurnContext;
            Assert.IsTrue(turnContext.HasValidCreditFor(undeadCard));
            turnContext.ConsumeCreditFor(undeadCard);
            scenario.Dispatch(new PromoteCommand(undeadCard));

            Assert.Contains(undeadCard, red.InnerCircle);
            Assert.AreEqual(0, turnContext.PendingPromotionsCount);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void ReturnTroopCommand_TargetingANonexistentNode_IsRejectedWhileHighPriestEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);

            var forgedCommand = new ReturnTroopCommand(999999, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayHighPriestOfMyrkulCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "high_priest_of_myrkul");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "High Priest of Myrkul should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void ReturnTroopCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueTroopsBefore = blue.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new ReturnTroopCommand(target.Id, card.Id));

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(blueTroopsBefore + 1, blue.TroopsInBarracks, "The barracks credit must not have been applied twice.");
        }

        [TestMethod]
        public void ReturnAnySpyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = SetupCleanEnemySpySite(scenario, red);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueSpiesBefore = blue.SpiesInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new ReturnAnySpyCommand(spySite.Id, PlayerColor.Blue, card.Id));

            Assert.DoesNotContain(PlayerColor.Blue, spySite.Spies);
            Assert.AreEqual(blueSpiesBefore + 1, blue.SpiesInBarracks, "The barracks credit must not have been applied twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void ReturnTroopCommand_DtoRoundTrip_StillReturnsTheTroopThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");
            scenario.PlayCard(card);

            var command = new ReturnTroopCommand(target.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as ReturnTroopCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
        }

        [TestMethod]
        public void ReturnAnySpyCommand_DtoRoundTrip_StillReturnsTheSpyThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var spySite = SetupCleanEnemySpySite(scenario, red);
            var card = scenario.GiveCard(PlayerColor.Red, "high_priest_of_myrkul");
            scenario.PlayCard(card);

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
