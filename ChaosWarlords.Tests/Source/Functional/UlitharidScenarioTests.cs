using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario coverage for PlayFromMarketCommand (see planning.txt TIER 1 - the 2026-09-01
    /// coverage run flagged it at 71% line / 50% branch). Ulitharid ("play a card in the
    /// market that costs 4 or less as if it was in your hand, then devour that card") is the
    /// only shipped card driving it.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class UlitharidScenarioTests
    {
        [TestMethod]
        public void PlayUlitharid_PlayingACheapMarketCard_ResolvesItThenDevoursIt()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var ulitharid = scenario.GiveCard(PlayerColor.Red, "ulitharid");
            var houseGuard = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            houseGuard.Location = CardLocation.Market;
            scenario.Context.MarketManager.MarketRow.Add(houseGuard); // cost 3 <= 4 - a valid target.

            scenario.PlayCard(ulitharid);

            Assert.AreEqual(ActionState.TargetingPlayFromMarket, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(ulitharid, scenario.Context.ActionSystem.PendingCard);

            var command = new PlayFromMarketCommand(houseGuard, ulitharid);
            scenario.Dispatch(command);

            // House Guard's own effect ("Gain 2 Power") should have resolved AS IF it was in
            // Red's hand, not Ulitharid's own effect.
            Assert.AreEqual(2, red.Power, "The market card's own effect should have resolved for the player who played Ulitharid.");
            Assert.DoesNotContain(houseGuard, scenario.Context.MarketManager.MarketRow, "House Guard should have left the market row.");
            Assert.AreEqual(CardLocation.Void, houseGuard.Location, "House Guard should end in the Void, not back in the market.");
            Assert.Contains(houseGuard, scenario.Context.VoidPile);
            Assert.DoesNotContain(houseGuard, red.Hand, "The market card must never have actually entered Red's hand.");
            Assert.DoesNotContain(houseGuard, red.PlayedCards, "Nor Red's PlayedCards - CleanUpTurn must not try to discard a card that's already devoured.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayFromMarketCommand_ForACardCostingMoreThanTheLimit_IsRejectedWithNoStateChange()
        {
            // Adversarial scenario: a forged/stale PlayFromMarketCommand naming a market card
            // over the cost limit must be rejected server-side (Validate() re-checks cost, not
            // trusting the client's own market-UI filter).
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            var ulitharid = scenario.GiveCard(PlayerColor.Red, "ulitharid");
            var neogi = scenario.CardDatabase.GetCardById("neogi", scenario.Context.Random)!; // Cost 7 > 4.
            neogi.Location = CardLocation.Market;
            scenario.Context.MarketManager.MarketRow.Add(neogi);

            scenario.PlayCard(ulitharid);
            Assert.AreEqual(ActionState.TargetingPlayFromMarket, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new PlayFromMarketCommand(neogi, ulitharid);
            scenario.AssertRejected(forgedCommand);

            Assert.Contains(neogi, scenario.Context.MarketManager.MarketRow, "The over-cost market card must remain untouched.");
            Assert.AreEqual(ActionState.TargetingPlayFromMarket, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real, valid selection.");
        }

        [TestMethod]
        public void PlayUlitharidCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var ulitharid = scenario.GiveCard(PlayerColor.Red, "ulitharid");
            var houseGuard = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            houseGuard.Location = CardLocation.Market;
            scenario.Context.MarketManager.MarketRow.Add(houseGuard); // Guarantees a valid target - cost 3 <= 4.

            scenario.DispatchTwice(new PlayCardCommand(ulitharid));

            Assert.AreEqual(ActionState.TargetingPlayFromMarket, scenario.Context.ActionSystem.CurrentState, "Should still be waiting for exactly the one market-card selection the first play triggered.");
        }
    }
}
