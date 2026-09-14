using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Blackguard ("Choose one: Gain 2 Power. Or, assassinate
    /// a troop.") and Inquisitor ("Choose one: Gain 2 Influence. Or, assassinate a troop." - same
    /// choose-one shape, different resource) - planning.txt TIER 1 item 8. Both are a plain
    /// GainResource/Assassinate choose-one, the same established shape as Wight's
    /// Devour/GainResource Alternative pair. Loads the REAL "blackguard"/"inquisitor" entries out
    /// of the REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// Batched into one file since the two cards share the same targeting/choose-one structure.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BlackguardInquisitorScenarioTests
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

        // --- Row 1: positive/happy path, accept branch ---

        [TestMethod]
        public void PlayBlackguard_AcceptGainResource_GainsTwoPowerWithoutTargeting()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "blackguard");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Gain 2 Power vs. Assassinate.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(2, red.Power);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 2: Choose-one mutual exclusivity, the OTHER direction ---

        [TestMethod]
        public void PlayBlackguard_DeclineGainResource_AssassinatesTheChosenTroopAndGrantsNoPower()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "blackguard");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(0, red.Power, "The declined GainResource branch must not also apply.");
        }

        // --- Row 3: no-valid-target fallback for the Assassinate branch ---

        [TestMethod]
        public void PlayBlackguard_NoTroopsAnywhere_DeclineFallsThroughCleanlyWithNoState()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "blackguard");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No Assassinate target anywhere - must resolve cleanly instead of stalling.");
            Assert.AreEqual(0, red.Power);
            Assert.AreEqual(0, red.TrophyHall);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayBlackguardCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "blackguard");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Blackguard should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileBlackguardEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "blackguard");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new AssassinateCommand(999999, card.Id), "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 6: unmet resource/requirement precondition - N/A. Neither branch has a resource
        // cost of its own to fail; the choose-one popup itself has no precondition beyond having
        // been dealt the card. ---

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayBlackguardCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "blackguard");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Inquisitor: same choose-one shape as Blackguard, but the accept branch grants
        // Influence instead of Power. ---

        [TestMethod]
        public void PlayInquisitor_AcceptGainResource_GainsTwoInfluenceWithoutTargeting()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "inquisitor");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(2, red.Influence);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayInquisitor_DeclineGainResource_AssassinatesTheChosenTroopAndGrantsNoInfluence()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "inquisitor");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(0, red.Influence);
        }

        [TestMethod]
        public void PlayInquisitorCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "inquisitor");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayInquisitorCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "inquisitor");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip (AssassinateCommand's DTO shape is already covered generically
        // elsewhere - RevenantScenarioTests/CommandSerializationTests - not repeated per card here). ---
    }
}
