using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Weaponmaster ("Choose three times: Deploy a troop. Or,
    /// Assassinate a white troop.") - the first shipped card using CardEffect.ChooseCount
    /// (ChooseCountChainTests.cs proves the underlying primitive directly via a hand-built card;
    /// this proves the same mechanism through the REAL "weaponmaster" cards.json entry and
    /// localization). Loads the real card and dispatches every command through a real
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class WeaponmasterScenarioTests
    {
        private static (Player red, MapNode t1, MapNode t2, MapNode t3) SetupRedWithThreeReachableNeutralTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 3);
            site.AddSpy(red.Color);
            var nodes = site.NodesInternal.Take(3).ToList();
            nodes[0].Occupant = PlayerColor.Neutral;
            nodes[1].Occupant = PlayerColor.Neutral;
            nodes[2].Occupant = PlayerColor.Neutral;
            return (red, nodes[0], nodes[1], nodes[2]);
        }

        // --- Row 1: positive/happy path, all 3 rounds accept Deploy ---

        [TestMethod]
        public void PlayWeaponmaster_AcceptingAllThreeRounds_CreditsThreePendingFreeTroops()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            int pendingBefore = red.PendingFreeTroops;
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(pendingBefore + 3, red.PendingFreeTroops);
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(PlayerColor.Neutral, t1.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t3.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 1b: positive/happy path, all 3 rounds decline into Assassinate ---

        [TestMethod]
        public void PlayWeaponmaster_DecliningAllThreeRounds_AssassinatesThreeReachableNeutralTroops()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(t1, null);

            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t2, null);

            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t3, null);

            Assert.AreEqual(3, red.TrophyHall);
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(PlayerColor.None, t2.Occupant);
            Assert.AreEqual(PlayerColor.None, t3.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 2: choose-one mutual exclusivity, mixed across the 3 rounds ---

        [TestMethod]
        public void PlayWeaponmaster_MixedChoices_EachRoundResolvesIndependently()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            int pendingBefore = red.PendingFreeTroops;
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true); // Round 1: Deploy.
            scenario.RespondToLatestInteraction(accept: false); // Round 2: Assassinate.
            scenario.ClickTarget(t2, null);
            scenario.RespondToLatestInteraction(accept: true); // Round 3: Deploy.

            Assert.AreEqual(pendingBefore + 2, red.PendingFreeTroops);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(PlayerColor.Neutral, t1.Occupant, "Round 1 chose Deploy - t1 was never targeted.");
            Assert.AreEqual(PlayerColor.None, t2.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t3.Occupant, "Round 3 chose Deploy - t3 was never targeted.");
        }

        // --- Row 3: no-valid-target fallback - Deploy is always legal, so declining into a
        // dead Assassinate just wastes that one round rather than losing the rest ---

        [TestMethod]
        public void PlayWeaponmaster_DeclineWithNoNeutralTroopsAnywhere_StillOffersAllThreeRounds()
        {
            var scenario = MatchScenario.Build();
            scenario.ClearNeutralTroopsFromBoard(); // Establish the "no Neutral troop anywhere" precondition this test is actually about.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int pendingBefore = red.PendingFreeTroops;
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false); // Nothing to assassinate anywhere.

            Assert.HasCount(2, scenario.Interactions, "Round 2 must still be offered despite round 1's decline finding no target.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.HasCount(3, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(pendingBefore + 2, red.PendingFreeTroops);
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayWeaponmasterCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "weaponmaster");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Weaponmaster should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions);
        }

        // --- Row 5: stale/nonexistent/already-assassinated target mid-sequence ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForALaterRound()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, _) = SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t1, null);
            Assert.AreEqual(1, red.TrophyHall);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(t1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "t1 is already empty - must be rejected as a target for round 2.");
            Assert.AreEqual(1, red.TrophyHall);

            scenario.ClickTarget(t2, null);
            Assert.AreEqual(2, red.TrophyHall);
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileWeaponmasterEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, _) = SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.DispatchTwice(new AssassinateCommand(t1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "t1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant, "t2 must remain untouched by the rejected replay.");

            // The rejected replay must not have consumed/corrupted round 2's own popup.
            Assert.HasCount(2, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t2, null);
            Assert.AreEqual(2, red.TrophyHall);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheFirstTargetThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, _, _) = SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "weaponmaster");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var command = new AssassinateCommand(t1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into an AssassinateCommand.");
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }
    }
}
