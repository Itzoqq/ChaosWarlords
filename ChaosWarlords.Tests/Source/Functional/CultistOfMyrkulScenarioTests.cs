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
        }

        [TestMethod]
        public void PlayCultist_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var cultist = scenario.GiveCard(PlayerColor.Blue, "cultist_of_myrkul"); // Belongs to Blue, not the active player.
            long sequenceBefore = scenario.Context.SequenceNumber;

            scenario.PlayCard(cultist);

            Assert.Contains(cultist, blue.Hand, "Cultist should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions, "No popup should have been raised for a rejected command.");
            Assert.AreEqual(0, red.Influence);
            Assert.AreEqual(sequenceBefore, scenario.Context.SequenceNumber, "A rejected command must not advance SequenceNumber.");
        }
    }
}
