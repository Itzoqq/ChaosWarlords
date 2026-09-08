using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Death Tyrant ("Assassinate up to 3 troops at a single
    /// site. For each troop removed, gain Influence.") - the same AllowPartialRepeat +
    /// RestrictRepeatsToFirstTargetSite shape Minotaur Skeleton already established for
    /// Assassinate (unrestricted troop color here, unlike Minotaur Skeleton's Neutral-only), plus
    /// the new CardEffect.GainResourcePerRepeat primitive: ActionSystem.PerformAssassinate grants
    /// 1 Influence EACH TIME a repeat actually succeeds, as it happens - not a single dynamic
    /// amount computed once after every repeat finishes (CardEffect.DynamicAmountSource's shape,
    /// used by White Dragon/Beholder/etc., would be the wrong tool here since it can't see "how
    /// many repeats of THIS action actually completed"). Loads the REAL "death_tyrant" entry out
    /// of the REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DeathTyrantScenarioTests
    {
        /// <summary>
        /// Grants Red Presence at a site with >= 3 nodes (via a spy) and marks 3 of its nodes
        /// Blue, plus Presence + 1 Blue node at a SECOND, different site - the setup every
        /// site-scoping test below needs.
        /// </summary>
        private static (Player red, Site siteA, MapNode a1, MapNode a2, MapNode a3, Site siteB, MapNode b1) SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 3);
            siteA.AddSpy(red.Color);
            var aNodes = siteA.NodesInternal.Take(3).ToList();
            aNodes[0].Occupant = PlayerColor.Blue;
            aNodes[1].Occupant = PlayerColor.Blue;
            aNodes[2].Occupant = PlayerColor.Blue;

            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            siteB.AddSpy(red.Color);
            var b1 = siteB.NodesInternal[0];
            b1.Occupant = PlayerColor.Blue;

            return (red, siteA, aNodes[0], aNodes[1], aNodes[2], siteB, b1);
        }

        // --- Row 1: happy path, all 3 repeats at siteA, Influence granted per removal ---

        [TestMethod]
        public void PlayDeathTyrant_ThreeEnemyTroopsAtOneSite_AssassinatesAllThreeAndGrantsThreeInfluence()
        {
            var scenario = MatchScenario.Build();
            var (red, siteA, a1, a2, a3, _, b1) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            int influenceBefore = red.Influence;
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            var pendingEffect = scenario.Context.ActionSystem.CurrentSourceEffect!;
            Assert.IsFalse(pendingEffect.TargetNeutralTroopOnly, "Death Tyrant's Assassinate is unrestricted - any troop, not just white ones.");
            Assert.IsTrue(pendingEffect.AllowPartialRepeat);
            Assert.IsTrue(pendingEffect.RestrictRepeatsToFirstTargetSite);
            Assert.AreEqual(ResourceType.Influence, pendingEffect.GainResourcePerRepeat);
            Assert.IsNull(scenario.Context.ActionSystem.PendingSite, "Not bound yet - no repeat has resolved.");

            scenario.ClickTarget(a1, null);
            Assert.AreEqual(PlayerColor.None, a1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(influenceBefore + 1, red.Influence, "1 Influence per troop removed - the first.");
            Assert.AreEqual(siteA, scenario.Context.ActionSystem.PendingSite, "The first repeat's site must now be bound.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(a2, null);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(influenceBefore + 2, red.Influence);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "One more repeat still owed.");

            scenario.ClickTarget(a3, null);
            Assert.AreEqual(3, red.TrophyHall);
            Assert.AreEqual(influenceBefore + 3, red.Influence, "All 3 removals should have granted Influence.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, b1.Occupant, "siteB's troop was never a legal target for this card's repeats.");
        }

        // --- Site-scoping: reused from Minotaur Skeleton's established coverage, applied here ---

        [TestMethod]
        public void DeathTyrant_ClickingASecondSiteAfterTheFirstTarget_IsRejectedByThePendingSiteGuard()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, b1) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall, "Setup check: first repeat resolved at siteA.");

            var rejected = scenario.ClickTarget(b1, null);

            Assert.IsNull(rejected, "A click at a different site must be rejected by the PendingSite guard.");
            Assert.AreEqual(PlayerColor.Blue, b1.Occupant, "siteB's troop must survive untouched.");
            Assert.AreEqual(1, red.TrophyHall, "No second trophy should have been granted.");
        }

        [TestMethod]
        public void AssassinateCommand_ForgedAgainstADifferentSite_IsRejectedByValidateEvenBypassingTheInputController()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, b1) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall);

            var forgedCommand = new AssassinateCommand(b1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A node at a different site than PendingSite must be rejected by Validate(), not just the input controller.");

            Assert.AreEqual(PlayerColor.Blue, b1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayDeathTyrant_NoTroopsAnywhereOnTheBoard_SkipsAssassinateEntirelyAndGrantsNoInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int influenceBefore = red.Influence;
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No legal target anywhere means Assassinate should skip entirely.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(influenceBefore, red.Influence);
        }

        [TestMethod]
        public void PlayDeathTyrant_OnlyTwoEnemyTroopsAtTheBoundSite_ResolvesAfterTwoDespiteAnotherTroopElsewhere()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int influenceBefore = red.Influence;

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            siteA.AddSpy(red.Color);
            var a1 = siteA.NodesInternal[0];
            var a2 = siteA.NodesInternal[1];
            a1.Occupant = PlayerColor.Blue;
            a2.Occupant = PlayerColor.Blue;

            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            siteB.AddSpy(red.Color);
            var b1 = siteB.NodesInternal[0];
            b1.Occupant = PlayerColor.Blue;

            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");
            scenario.PlayCard(card);

            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "One more repeat still owed.");

            scenario.ClickTarget(a2, null);

            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(influenceBefore + 2, red.Influence, "Exactly 2 removals should have granted exactly 2 Influence, not clamped/forced to 3.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "siteA is exhausted - must resolve early instead of waiting for an impossible 3rd.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Blue, b1.Occupant, "siteB's troop must survive - it was never a legal target for this sequence.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayDeathTyrantCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "death_tyrant");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Death Tyrant should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, blue.Influence);
        }

        // --- Row 5: stale/nonexistent/already-moved target ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForTheSecondRepeat()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, _) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall);

            var forgedCommand = new AssassinateCommand(a1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-assassinated (now empty) node must be rejected as a target for the 2nd repeat.");
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileDeathTyrantEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Voluntary partial decline (AllowPartialRepeat's "up to 3") ---

        [TestMethod]
        public void DeclineRepeatCommand_BeforeAssassinatingAnything_KeepsInfluenceAtZero()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, a2, a3, _, _) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            int influenceBefore = red.Influence;
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            Assert.IsNull(scenario.Context.ActionSystem.PendingSite);

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(0, red.TrophyHall, "Nothing should have been assassinated - the player declined immediately.");
            Assert.AreEqual(influenceBefore, red.Influence, "No repeats resolved - no Influence should have been granted.");
            Assert.AreEqual(PlayerColor.Blue, a1.Occupant);
            Assert.AreEqual(PlayerColor.Blue, a2.Occupant);
            Assert.AreEqual(PlayerColor.Blue, a3.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void DeclineRepeatCommand_AfterOneAssassination_KeepsTheFirstInfluenceAndStopsThere()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, a2, _, _, _) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            int influenceBefore = red.Influence;
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "2 more repeats still legally owed.");

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The already-resolved first assassination must be KEPT, not rolled back.");
            Assert.AreEqual(influenceBefore + 1, red.Influence, "The already-granted Influence from the first repeat must be KEPT.");
            Assert.AreEqual(PlayerColor.Blue, a2.Occupant, "a2 was never touched - declined before being targeted.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejectedAndDoesNotDoubleGrantInfluence()
        {
            var scenario = MatchScenario.Build();
            var (red, siteA, a1, a2, _, _, _) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            int influenceBefore = red.Influence;
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new AssassinateCommand(a1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "a1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(influenceBefore + 1, red.Influence, "Influence should have been granted exactly once, not twice.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed.");
            Assert.AreEqual(siteA, scenario.Context.ActionSystem.PendingSite, "The rejected replay must not have re-bound or cleared PendingSite.");
            Assert.AreEqual(PlayerColor.Blue, a2.Occupant, "a2 must remain untouched by the rejected replay.");

            scenario.ClickTarget(a2, null);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(influenceBefore + 2, red.Influence);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheFirstTargetAndGrantsInfluenceThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, _) = SetupRedWithThreeEnemyTroopsAtOneSiteAndOneAtAnother(scenario);
            int influenceBefore = red.Influence;
            var card = scenario.GiveCard(PlayerColor.Red, "death_tyrant");
            scenario.PlayCard(card);

            var command = new AssassinateCommand(a1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into an AssassinateCommand.");
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, a1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(influenceBefore + 1, red.Influence);
        }
    }
}
