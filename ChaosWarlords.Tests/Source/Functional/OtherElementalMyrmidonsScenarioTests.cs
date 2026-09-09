using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Lighter scenario-harness matrix pass for Fire/Water/Earth Elemental Myrmidon - siblings of
    /// Air Elemental Myrmidon (see AirElementalMyrmidonScenarioTests.cs for the deep,
    /// end-to-end proof of the new CardEffect.RequiredPromotionAspect aspect-filter primitive
    /// itself, and TurnContextTests.cs/PromoteInputModeTests.cs for the filter mechanism in
    /// isolation - not repeated 3 more times here). Each card here is just a different immediate
    /// effect (GainResource/Assassinate) followed by the same already-proven Promote-credit
    /// banking, so only rows 1 (positive), 4 (wrong-player), and 7 (double-dispatch) apply, plus
    /// row 3 (no-valid-target fallback) for Water's Assassinate half - matching
    /// TrivialPrimitiveCardsScenarioTests.cs's own batching precedent for near-identical cards.
    /// Earth Elemental Myrmidon is the control case: its Promote credit is UNFILTERED (plain
    /// "promote another card played this turn", core_noble's exact shape) - included here to
    /// prove the new RequiredPromotionAspect field genuinely defaults to "no filter" and doesn't
    /// leak into a card that never sets it.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class OtherElementalMyrmidonsScenarioTests
    {
        // --- fire_elemental_myrmidon: "Gain 2 Power. At end of turn, promote an Obedience
        // card played this turn." - both effects automatic, no targeting at all. ---

        [TestMethod]
        public void PlayFireElementalMyrmidon_GrantsTwoPower_AndBanksAnObedienceFilteredCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "fire_elemental_myrmidon");
            var orderCard = scenario.GiveCard(PlayerColor.Red, "test_guard"); // Aspect.Order.
            scenario.PlayCard(orderCard);

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Power);
            var context = scenario.Context.TurnManager.CurrentTurnContext;
            Assert.AreEqual(1, context.PendingPromotionsCount);
            Assert.IsTrue(context.HasValidCreditFor(orderCard), "The banked credit must be filtered to Order, and test_guard is Order.");
        }

        [TestMethod]
        public void PlayFireElementalMyrmidonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "fire_elemental_myrmidon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.AreEqual(0, blue.Power);
        }

        [TestMethod]
        public void PlayFireElementalMyrmidonCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "fire_elemental_myrmidon");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.Power, "Should have applied exactly once, not twice.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The credit must have been banked exactly once, not twice.");
        }

        // --- water_elemental_myrmidon: "Assassinate a white troop. At end of turn, promote an
        // Obedience card played this turn." ---

        /// <summary>
        /// Deploys Red at a real node and marks an adjacent node with <paramref name="occupant"/>
        /// - matches RavenousZombiesScenarioTests.cs's identical helper (Assassinate needs
        /// Presence at the target's node; no "anywhere on the board" wording on this card).
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

        [TestMethod]
        public void PlayWaterElementalMyrmidon_WithNeutralTroopPresent_AssassinatesIt_AndBanksAnObedienceFilteredCredit()
        {
            var scenario = MatchScenario.Build();
            var (red, neutralTarget) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "water_elemental_myrmidon");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(neutralTarget, null);

            Assert.AreEqual(PlayerColor.None, neutralTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayWaterElementalMyrmidon_TargetingAnActualPlayersTroop_IsRejected()
        {
            // AssassinateCommand.Validate() independently re-derives the neutral-only
            // restriction (see RavenousZombiesScenarioTests.cs's own doc comment) - a
            // player-owned troop must be rejected even via a hand-forged command. Needs a REAL
            // Neutral troop present too (not just the Blue one), or the pre-push HasValidTargets
            // lookahead skips Assassinate entirely before targeting even starts (see the
            // no-valid-target-fallback test above) - matching RavenousZombiesScenarioTests.cs's
            // own identical setup (a 2-node site + a spy grants Red Presence at both).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            var neutralNode = site.NodesInternal[0];
            var blueTarget = site.NodesInternal[1];
            neutralNode.Occupant = PlayerColor.Neutral;
            blueTarget.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color); // Setup only - grants Presence at every node of this site.

            var card = scenario.GiveCard(PlayerColor.Red, "water_elemental_myrmidon");
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "A real Neutral target exists, so targeting must have started.");

            var forgedCommand = new AssassinateCommand(blueTarget.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "A non-Neutral troop must be rejected as a target for this filtered Assassinate.");
            Assert.AreEqual(PlayerColor.Blue, blueTarget.Occupant, "The rejected command must not have assassinated anything.");
        }

        [TestMethod]
        public void PlayWaterElementalMyrmidon_WithNoNeutralTroopAnywhere_StillBanksThePromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "water_elemental_myrmidon"); // No troops anywhere on the board.

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Assassinate should have quietly no-opped - no targeting UI should ever have opened.");
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The Promote half must still apply independently of the Assassinate half.");
        }

        [TestMethod]
        public void PlayWaterElementalMyrmidonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "water_elemental_myrmidon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayWaterElementalMyrmidonCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "water_elemental_myrmidon"); // No neutral troop - resolves instantly.

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The credit must have been banked exactly once, not twice.");
        }

        // --- earth_elemental_myrmidon: "Gain 2 Influence. At end of turn, promote another
        // card played this turn." - the UNFILTERED control case. ---

        [TestMethod]
        public void PlayEarthElementalMyrmidon_GrantsTwoInfluence_AndBanksAnUnfilteredCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "earth_elemental_myrmidon");
            // A card of a completely different aspect - must still be a valid target, since
            // this card's own Promote credit carries no RequiredPromotionAspect at all.
            var shadowCard = scenario.GiveCard(PlayerColor.Red, "drow_spy_master"); // Aspect.Shadow.
            var site = scenario.Context.MapManager.Sites.First();
            scenario.PlayCard(shadowCard);
            scenario.ClickTarget(null, site);

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Influence);
            var context = scenario.Context.TurnManager.CurrentTurnContext;
            Assert.AreEqual(1, context.PendingPromotionsCount);
            Assert.IsTrue(context.HasValidCreditFor(shadowCard), "An unfiltered credit must accept a card of ANY aspect, not just Order.");
        }

        [TestMethod]
        public void PlayEarthElementalMyrmidonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "earth_elemental_myrmidon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.AreEqual(0, blue.Influence);
        }

        [TestMethod]
        public void PlayEarthElementalMyrmidonCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "earth_elemental_myrmidon");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.Influence, "Should have applied exactly once, not twice.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The credit must have been banked exactly once, not twice.");
        }
    }
}
