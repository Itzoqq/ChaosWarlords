using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Minotaur Skeleton ("Choose one: Deploy 3 troops. Or,
    /// devour this card to assassinate up to 3 white troops at a single site.") - the first
    /// shipped card using CardEffect.RestrictRepeatsToFirstTargetSite, a new sibling constraint
    /// to AllowPartialRepeat/SupportsRepeat: the first repeat of the chained Assassinate can hit
    /// any legal Neutral troop, but ActionSystem.PerformAssassinate then binds ActionSystem.
    /// PendingSite to that target's site, so every later repeat of THIS card's effect is confined
    /// to that same site (reusing the PendingSite guard Cloaker's ReturnOwnSpy->Assassinate chain
    /// already established). Also the first card to exercise a mandatory (non-optional) Devour
    /// node reached via a DECLINED top-level optional GainResource's Alternative (Kobold's
    /// Choose-one shape, one level deeper) - see CardEffect.RestrictRepeatsToFirstTargetSite's and
    /// ActionSystem.PerformAssassinate's doc comments. Loads the REAL "minotaur_skeleton" entry
    /// out of the REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MinotaurSkeletonScenarioTests
    {
        /// <summary>
        /// Grants Red Presence at a site with >= 3 nodes (via a spy - HasSpyPresence covers
        /// every node at that site, matching RavenousZombiesScenarioTests/CloakerScenarioTests'
        /// own pattern) and marks 3 of its nodes Neutral, plus Presence + 1 Neutral node at a
        /// SECOND, different site - the setup every site-scoping test below needs.
        /// </summary>
        private static (Player red, Site siteA, MapNode a1, MapNode a2, MapNode a3, Site siteB, MapNode b1) SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 3);
            siteA.AddSpy(red.Color);
            var aNodes = siteA.NodesInternal.Take(3).ToList();
            aNodes[0].Occupant = PlayerColor.Neutral;
            aNodes[1].Occupant = PlayerColor.Neutral;
            aNodes[2].Occupant = PlayerColor.Neutral;

            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            siteB.AddSpy(red.Color);
            var b1 = siteB.NodesInternal[0];
            b1.Occupant = PlayerColor.Neutral;

            return (red, siteA, aNodes[0], aNodes[1], aNodes[2], siteB, b1);
        }

        // --- Row 2 (choose-one, accept direction): Deploy branch ---

        [TestMethod]
        public void PlayMinotaurSkeleton_AcceptTheOptionalDeploy_CreditsPendingFreeTroopsAndNeverDevours()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, b1) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            int pendingBefore = red.PendingFreeTroops;
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Deploy vs. the Devour->Assassinate Alternative.");

            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(pendingBefore + 3, red.PendingFreeTroops, "Accepting should credit exactly 3 pending free troops.");
            Assert.AreEqual(CardLocation.Played, card.Location, "Not devoured - the Alternative branch never fired.");
            Assert.DoesNotContain(card, scenario.Context.CardsMarkedForTurnEndDevour);
            Assert.AreEqual(PlayerColor.Neutral, a1.Occupant, "The Assassinate Alternative must never fire when Deploy is accepted.");
            Assert.AreEqual(PlayerColor.Neutral, b1.Occupant);
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);

            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(CardLocation.DiscardPile, card.Location, "Never devoured - discards normally like any other played card.");
        }

        // --- Row 2 (choose-one, decline direction) + Row 1: happy path, all 3 repeats at siteA ---

        [TestMethod]
        public void PlayMinotaurSkeleton_DeclineTheOptionalDeploy_DevoursThenAssassinatesAllThreeAtSiteA()
        {
            var scenario = MatchScenario.Build();
            var (red, siteA, a1, a2, a3, _, b1) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false);

            // The Devour(Self) node is MANDATORY (not itself IsOptional) once the Alternative is
            // reached - it should already have resolved with no further popup/click.
            Assert.AreEqual(CardLocation.Played, card.Location, "Self-devour stays 'Played' until end of turn, same as Skeletal Horde/Zuggtmoy/Revenant's deferred pattern.");
            Assert.Contains(card, scenario.Context.CardsMarkedForTurnEndDevour);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            var pendingEffect = scenario.Context.ActionSystem.CurrentSourceEffect!;
            Assert.IsTrue(pendingEffect.TargetNeutralTroopOnly);
            Assert.IsTrue(pendingEffect.AllowPartialRepeat);
            Assert.IsTrue(pendingEffect.RestrictRepeatsToFirstTargetSite);
            Assert.IsNull(scenario.Context.ActionSystem.PendingSite, "Not bound yet - no repeat has resolved.");

            scenario.ClickTarget(a1, null);
            Assert.AreEqual(PlayerColor.None, a1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(siteA, scenario.Context.ActionSystem.PendingSite, "The first repeat's site must now be bound.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(a2, null);
            Assert.AreEqual(PlayerColor.None, a2.Occupant);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "One more repeat still owed.");

            scenario.ClickTarget(a3, null);
            Assert.AreEqual(PlayerColor.None, a3.Occupant);
            Assert.AreEqual(3, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Neutral, b1.Occupant, "siteB's troop was never a legal target for this card's repeats.");

            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(CardLocation.Void, card.Location);
            Assert.Contains(card, scenario.Context.VoidPile);
            Assert.IsEmpty(scenario.Context.CardsMarkedForTurnEndDevour);
        }

        // --- Site-scoping: the core new behavior under test ---

        [TestMethod]
        public void MinotaurSkeleton_ClickingASecondSiteAfterTheFirstTarget_IsRejectedByThePendingSiteGuard()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, b1) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall, "Setup check: first repeat resolved at siteA.");

            var rejected = scenario.ClickTarget(b1, null);

            Assert.IsNull(rejected, "A click at a different site must be rejected by the PendingSite guard, exactly like Cloaker's chain.");
            Assert.AreEqual(PlayerColor.Neutral, b1.Occupant, "siteB's troop must survive untouched.");
            Assert.AreEqual(1, red.TrophyHall, "No second trophy should have been granted.");
        }

        [TestMethod]
        public void AssassinateCommand_ForgedAgainstADifferentSite_IsRejectedByValidateEvenBypassingTheInputController()
        {
            // Defense-in-depth: Validate() must independently re-derive and enforce the same
            // PendingSite restriction ActionInputController.HandleAssassinate already enforces at
            // the UI layer - a forged command dispatched directly (bypassing ClickTarget/the
            // input layer entirely) must be rejected too.
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, b1) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall);

            var forgedCommand = new AssassinateCommand(b1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A node at a different site than PendingSite must be rejected by Validate(), not just the input controller.");

            Assert.AreEqual(PlayerColor.Neutral, b1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void PlayMinotaurSkeleton_OnlyTwoNeutralTroopsAtTheBoundSite_ResolvesAfterTwoDespiteAnotherNeutralTroopElsewhere()
        {
            // Proves AssassinateStrategy.HasValidTargets's restrictToSite wiring end-to-end, not
            // just via its own unit test: siteB's Neutral troop is a perfectly legal Assassinate
            // target in the abstract (Presence granted, Neutral) but must NOT count toward "are
            // there more legal targets" once PendingSite has bound this sequence to siteA.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var siteA = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            siteA.AddSpy(red.Color);
            var a1 = siteA.NodesInternal[0];
            var a2 = siteA.NodesInternal[1];
            a1.Occupant = PlayerColor.Neutral;
            a2.Occupant = PlayerColor.Neutral;

            var siteB = scenario.Context.MapManager.Sites.First(s => s != siteA && s.NodesInternal.Count > 0);
            siteB.AddSpy(red.Color);
            var b1 = siteB.NodesInternal[0];
            b1.Occupant = PlayerColor.Neutral;

            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "One more repeat still owed.");

            scenario.ClickTarget(a2, null);

            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "siteA is exhausted - must resolve early instead of waiting for an impossible 3rd, even though siteB still has a Neutral troop.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(PlayerColor.Neutral, b1.Occupant, "siteB's troop must survive - it was never a legal target for this sequence.");
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayMinotaurSkeleton_DeclineWithNoNeutralTroopsAnywhere_StillDevoursButSkipsAssassinateEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.Contains(card, scenario.Context.CardsMarkedForTurnEndDevour, "Devour(Self) is unconditional once the Alternative branch is reached - it always succeeds.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No legal Neutral target anywhere means Assassinate should skip entirely.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayMinotaurSkeletonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "minotaur_skeleton");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Minotaur Skeleton should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent/already-moved target ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForTheSecondRepeat()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, _) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(1, red.TrophyHall);

            var forgedCommand = new AssassinateCommand(a1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-assassinated (now empty) node must be rejected as a target for the 2nd repeat.");
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileMinotaurSkeletonEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Voluntary partial decline (AllowPartialRepeat's "up to 3") ---

        [TestMethod]
        public void DeclineRepeatCommand_BeforeAssassinatingAnything_KeepsTheDevourButAssassinatesZero()
        {
            // Zero repeats used is a legal outcome for "up to 3" - and specifically exercises
            // RestrictRepeatsToFirstTargetSite's PendingSite==null guard never having a chance
            // to bind at all, distinct from DeclineRepeatCommand_AfterOneAssassination below
            // (which declines AFTER PendingSite is already bound).
            var scenario = MatchScenario.Build();
            var (red, _, a1, a2, a3, _, _) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            Assert.IsNull(scenario.Context.ActionSystem.PendingSite);

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(0, red.TrophyHall, "Nothing should have been assassinated - the player declined immediately.");
            Assert.AreEqual(PlayerColor.Neutral, a1.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, a2.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, a3.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.IsNull(scenario.Context.ActionSystem.PendingSite, "Never bound - no repeat ever resolved.");
            Assert.Contains(card, scenario.Context.CardsMarkedForTurnEndDevour, "The self-devour from earlier in the chain must still stand even with zero Assassinate repeats used.");
        }

        [TestMethod]
        public void DeclineRepeatCommand_AfterOneAssassination_KeepsTheFirstAndStopsThere()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, a2, _, _, _) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(a1, null);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "2 more repeats still legally owed.");

            scenario.Dispatch(new DeclineRepeatCommand(card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The already-resolved first assassination must be KEPT, not rolled back.");
            Assert.AreEqual(PlayerColor.Neutral, a2.Occupant, "a2 was never touched - declined before being targeted.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.Contains(card, scenario.Context.CardsMarkedForTurnEndDevour, "The self-devour from earlier in the chain must be unaffected by declining the LATER Assassinate repeats.");
        }

        [TestMethod]
        public void DeclineRepeatCommand_WithAMismatchedCardId_IsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DeclineRepeatCommand("some_other_card"), "A decline referencing the wrong card's sequence must be rejected.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The rejected decline must not have touched the pending sequence.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, siteA, a1, a2, _, _, _) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.DispatchTwice(new AssassinateCommand(a1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "a1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed.");
            Assert.AreEqual(siteA, scenario.Context.ActionSystem.PendingSite, "The rejected replay must not have re-bound or cleared PendingSite.");
            Assert.AreEqual(PlayerColor.Neutral, a2.Occupant, "a2 must remain untouched by the rejected replay.");

            scenario.ClickTarget(a2, null);
            Assert.AreEqual(2, red.TrophyHall);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheFirstTargetThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, _, a1, _, _, _, _) = SetupRedWithThreeNeutralTroopsAtOneSiteAndOneAtAnother(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "minotaur_skeleton");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var command = new AssassinateCommand(a1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into an AssassinateCommand.");
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, a1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }
    }
}
