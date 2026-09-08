using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Beholder ("Assassinate a troop. Gain Influence for
    /// every 3 troops in your trophy hall.") - Assassinate(Amount: 1, no TargetNeutralTroopOnly
    /// restriction, same shape as Deathblade) followed by a second, non-targeting
    /// GainResource(Influence) effect using CardEffectProcessor's dynamic-amount mechanism
    /// (CardEffect.DynamicAmountSource.TrophyHallCount/DynamicAmountDivisor - the second shipped
    /// DynamicAmountSource value after White Dragon's SitesControlled). Both top-level effects
    /// resolve in array order (CardEffectProcessor.ResolveEffects), so the Influence half counts
    /// the trophy hall AFTER this card's own Assassinate has (or hasn't) added to it. Loads the
    /// REAL "beholder" entry out of the REAL cards.json and dispatches every command through a
    /// REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BeholderScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithAdjacentTroop(MatchScenario scenario, PlayerColor occupant)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var targetNode = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            targetNode.Occupant = occupant; // Setup only - not going through a command.

            return (red, targetNode);
        }

        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayBeholder_WithFiveTrophiesAlready_AssassinatesAndGrantsInfluenceCountingTheFreshSixthTrophy()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            red.SetTrophyHall(5, PlayerColor.Neutral); // Setup only - pre-existing trophies from earlier turns.
            var card = scenario.GiveCard(PlayerColor.Red, "beholder");
            int influenceBefore = red.Influence;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant, "The targeted troop should have been assassinated.");
            Assert.AreEqual(6, red.TrophyHall, "The freshly-assassinated troop must be counted alongside the 5 pre-existing ones.");
            Assert.AreEqual(influenceBefore + 2, red.Influence, "6 total troops / 3 per Influence = 2 Influence.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        [TestMethod]
        public void PlayBeholder_WithNoPriorTrophies_AssassinatesButFloorsInfluenceToZero()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "beholder");
            int influenceBefore = red.Influence;

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(1, red.TrophyHall, "Only the just-assassinated troop is in the trophy hall.");
            Assert.AreEqual(influenceBefore, red.Influence, "1 troop / 3 per Influence must floor to 0, not round up.");
        }

        // --- Row 3: no-valid-target fallback for the Assassinate half - the Influence half must
        // still fire, counting whatever was already in the trophy hall before this card. ---

        [TestMethod]
        public void PlayBeholder_NoTroopsAnywhereOnTheBoard_SkipsAssassinateButStillGrantsInfluenceFromExistingTrophyHall()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(6, PlayerColor.Neutral); // Setup only - pre-existing trophies from earlier turns.
            var card = scenario.GiveCard(PlayerColor.Red, "beholder");
            int influenceBefore = red.Influence;

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid targets anywhere means Assassinate should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(6, red.TrophyHall, "The trophy hall must be unchanged - Assassinate never fired.");
            Assert.AreEqual(influenceBefore + 2, red.Influence, "The Influence half is unrelated to whether Assassinate found a target and must still apply: 6 / 3 = 2.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayBeholderCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "beholder");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Beholder should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, blue.Influence);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileBeholderEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "beholder");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7/8: double-dispatch/replay and rapid dispatch ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameTarget_SecondDispatchIsRejectedAndDoesNotDoubleGrantInfluence()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            red.SetTrophyHall(5, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "beholder");
            int influenceBefore = red.Influence;

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new AssassinateCommand(target.Id, card.Id));

            Assert.AreEqual(6, red.TrophyHall, "Should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(influenceBefore + 2, red.Influence, "Influence should have been granted exactly once (6/3=2), not twice.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
