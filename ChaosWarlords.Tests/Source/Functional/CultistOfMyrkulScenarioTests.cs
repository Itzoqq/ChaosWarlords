using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Cultist of Myrkul (see planning.txt TIER 1 audit,
    /// 2026-09-01) - the second card (alongside Wight) with a real, session-found bug in the
    /// same Choose-one mutual-exclusivity class. CultistOfMyrkulMechanicsTests.cs already pins
    /// the ActionSystem-internal shape (hand-typed card, direct command.Execute(context)
    /// calls, mocked ICardDatabase) - this exercises the same "decline for Influence / accept
    /// to devour self and bank a promotion credit" sequence with the REAL "cultist_of_myrkul"
    /// cards.json entry, through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CultistOfMyrkulScenarioTests
    {
        [TestMethod]
        public void PlayCultist_DeclineDevour_GrantsInfluenceAlternative()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var cultist = scenario.GiveCard(PlayerColor.Red, "cultist_of_myrkul");
            scenario.PlayCard(cultist);

            Assert.HasCount(1, scenario.Interactions, "Self-devour is always a valid target (the card itself), so the popup should be requested.");
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(2, red.Influence, "Declining should grant the +2 Influence Alternative.");
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "Declining must not also bank a promotion credit.");
        }

        [TestMethod]
        public void PlayCultist_AcceptDevour_DevoursSelfAndBanksPromotionCredit_NotInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var cultist = scenario.GiveCard(PlayerColor.Red, "cultist_of_myrkul");
            scenario.PlayCard(cultist);
            scenario.RespondToLatestInteraction(accept: true);

            // Devour(Self) applies its OnSuccess immediately but defers the actual move-to-Void
            // until end of turn (CardsMarkedForTurnEndDevour) - matches Skeletal Horde's
            // established pattern, so the card stays visibly "in play" for the rest of the turn.
            Assert.AreEqual(CardLocation.Played, cultist.Location, "Self-devour stays 'Played' until end of turn.");
            Assert.Contains(cultist, scenario.Context.CardsMarkedForTurnEndDevour, "Card should be marked for end-of-turn devour.");
            Assert.AreEqual(2, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "Accepting should bank 2 Promote credits (\"promote up to 2\") for later, not resolve them immediately.");
            Assert.AreEqual(0, red.Influence, "Choose-one mutual exclusivity: accepting the devour must NOT also grant the +2 Influence Alternative.");
            // cards.json says PromotionCreditIsOptional: true for this effect - the runtime
            // TurnContext.PromotionCredit banked from it must actually carry that flag through
            // from JSON (CardFactory.ParseOptionalFlags), not just the hand-typed CardEffect
            // CultistOfMyrkulMechanicsTests.cs constructs directly.
            Assert.IsTrue(scenario.Context.TurnManager.CurrentTurnContext.CanDeclineRemainingPromotions,
                "\"Promote UP TO 2\" credits loaded from the real cards.json must be voluntarily declinable.");
        }

        [TestMethod]
        public void PlayCultist_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var cultist = scenario.GiveCard(PlayerColor.Blue, "cultist_of_myrkul"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(cultist));

            Assert.Contains(cultist, blue.Hand, "Cultist should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions, "No popup should have been raised for a rejected command.");
            Assert.AreEqual(0, red.Influence);
        }

        [TestMethod]
        public void PlayCultistCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var cultist = scenario.GiveCard(PlayerColor.Red, "cultist_of_myrkul");

            scenario.DispatchTwice(new PlayCardCommand(cultist));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }
    }
}
