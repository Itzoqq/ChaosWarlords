using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Mindwitness ("Assassinate a troop. If that troop belonged
    /// to another player and they have more than 3 cards, they must discard a card.") - the
    /// first shipped card using the OUTCOME-DEPENDENT TARGETING primitive (planning.txt TIER 1
    /// #3, the "architecturally trickiest" item on that list): CardEffect.TargetsAffectedPlayer
    /// + ActionSystem.PendingAffectedPlayerColor + CardEffectProcessor.PushEffectContext's new
    /// Condition-gating-for-targeting-effects check. Unlike Cranium Rats' SelectOpponent (the
    /// active player CHOOSES which opponent to force), Mindwitness's forced opponent is
    /// determined automatically by whoever the immediately preceding Assassinate step actually
    /// hit - there is no player choice involved. Loads the REAL "mindwitness" entry out of the
    /// REAL cards.json and dispatches every command through a REAL CommandDispatcher, mirroring
    /// CraniumRatsScenarioTests.cs/DeathbladeScenarioTests.cs's style.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MindwitnessScenarioTests
    {
        /// <summary>
        /// Deploys Red at a real node and marks an adjacent node with <paramref name="occupant"/> -
        /// Assassinate requires Presence, granted here via the deployed Red troop's adjacency.
        /// Same helper shape as RavenousZombiesScenarioTests.SetupRedWithAdjacentTroop.
        /// </summary>
        private static (Player red, MapNode targetNode) SetupRedWithAdjacentTroop(MatchScenario scenario, PlayerColor occupant)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var targetNode = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            targetNode.Occupant = occupant; // Setup only - not going through a command.

            return (red, targetNode);
        }

        private static void GiveHandOfSize(MatchScenario scenario, PlayerColor color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                scenario.GiveCard(color, "core_house_guard");
            }
        }

        // --- Row 1/2: positive/happy path + the condition's "if" branch actually firing ---

        [TestMethod]
        public void PlayMindwitness_AssassinatedTroopBelongsToOpponentWithMoreThanThreeCards_ForcesThatOpponentToDiscard()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var blue = scenario.Player(PlayerColor.Blue);
            var blueCard1 = scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            GiveHandOfSize(scenario, PlayerColor.Blue, 3); // Blue now has 4 cards - "more than 3".

            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant, "The troop should already be assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "The affected opponent's forced discard should have opened automatically.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "ActivePlayer should resolve to the AFFECTED player (forced), not whoever chose to play Mindwitness.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, blueCard1.Id));

            Assert.DoesNotContain(blueCard1, blue.Hand, "The affected opponent's card should have left their hand.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "ActivePlayer should have reverted to the real active player once the chain fully resolves.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "The forced-actor override must be fully released once the chain completes.");
        }

        // --- Row 2/6: the condition's "if" branch NOT firing - both ways it can be false ---

        [TestMethod]
        public void PlayMindwitness_AssassinatedTroopIsNeutral_NoDiscardTriggered()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant, "The neutral troop should still be assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "A white/unaligned troop belongs to no player - the discard effect must skip cleanly, not hang.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayMindwitness_AffectedOpponentHasExactlyThreeCards_NoDiscardTriggered()
        {
            // Boundary value: the card's own threshold is exclusive ("more than 3 cards") -
            // exactly 3 must NOT trigger the discard.
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var blue = scenario.Player(PlayerColor.Blue);
            GiveHandOfSize(scenario, PlayerColor.Blue, 3);

            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");
            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant, "The troop should still be assassinated.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Exactly 3 cards is not \"more than 3\" - the discard effect must skip cleanly.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "ActivePlayer must never have been force-switched at all for an ineligible opponent.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
            Assert.HasCount(3, blue.Hand, "Blue's hand must be untouched.");
        }

        // --- Row 3: no-valid-target fallback for Mindwitness's own Assassinate step ---

        [TestMethod]
        public void PlayMindwitness_NoTroopsAnywhereOnTheBoard_SkipsAssassinateEntirely()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid targets anywhere means the effect should skip entirely, not open targeting.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.IsEmpty(scenario.Interactions, "No optional-effect popup exists on this card.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayMindwitnessCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "mindwitness");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Mindwitness should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target, on BOTH chained steps ---

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileMindwitnessEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void DiscardCardCommand_ForANonexistentCardInTheForcedOpponentsHand_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var (_, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var blue = scenario.Player(PlayerColor.Blue);
            GiveHandOfSize(scenario, PlayerColor.Blue, 4);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DiscardCardCommand(blue.Color, "no-such-card-id"), "A nonexistent card id must be rejected.");
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real discard choice.");
        }

        [TestMethod]
        public void DiscardCardCommand_DispatchedByTheActivePlayerInsteadOfTheForcedOpponent_IsRejected()
        {
            // The real active player (Red, who played Mindwitness) cannot discard on the
            // forced opponent's behalf, nor can they discard from their OWN hand while
            // ActivePlayer currently resolves to Blue - DiscardCardCommand.Validate's
            // player != context.TurnManager.ActivePlayer check must reject this.
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var blue = scenario.Player(PlayerColor.Blue);
            GiveHandOfSize(scenario, PlayerColor.Blue, 4);
            var redCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DiscardCardCommand(red.Color, redCard.Id), "Red is not the player currently forced to discard.");
            Assert.Contains(redCard, red.Hand);
            Assert.HasCount(4, blue.Hand);
        }

        // --- Row 7: double-dispatch/replay, on BOTH chained steps ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new AssassinateCommand(target.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "The troop should have been assassinated exactly once, not twice.");
        }

        [TestMethod]
        public void MindwitnessDiscardCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (_, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var blue = scenario.Player(PlayerColor.Blue);
            var blueCard1 = scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            GiveHandOfSize(scenario, PlayerColor.Blue, 3);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new DiscardCardCommand(blue.Color, blueCard1.Id));

            Assert.DoesNotContain(blueCard1, blue.Hand);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
        }

        // --- Cancellation: the new BeginForcedActingPlayer call must be fully undone too ---

        [TestMethod]
        public void CancelTargeting_DuringMindwitnessForcedDiscard_RevertsAssassinationAndReleasesTheForcedActor()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var blue = scenario.Player(PlayerColor.Blue);
            GiveHandOfSize(scenario, PlayerColor.Blue, 4);
            var card = scenario.GiveCard(PlayerColor.Red, "mindwitness");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: mid-sequence, waiting on Blue's forced discard.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer);

            scenario.Context.ActionSystem.CancelTargeting();

            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "CancelTargeting must release the forced-actor override.");
            Assert.AreEqual(red, scenario.Context.ActivePlayer, "ActivePlayer should have reverted to the real active player.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(PlayerColor.Blue, target.Occupant, "The assassination itself must be undone by the cancel - EnsureTargetingSnapshot takes its snapshot before Assassinate ever resolves.");
            Assert.AreEqual(0, red.TrophyHall);
            Assert.HasCount(4, blue.Hand, "Blue's hand must be untouched - the discard never happened.");
            Assert.Contains(card.DefinitionId, red.Hand.Select(c => c.DefinitionId).ToList(), "The played card itself is restored to hand on cancel.");
        }
    }
}
