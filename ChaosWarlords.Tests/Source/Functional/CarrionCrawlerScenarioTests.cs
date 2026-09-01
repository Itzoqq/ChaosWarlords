using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Second scenario-harness migration target (see planning.txt TIER 1) - Carrion Crawler is
    /// the card whose mandatory market-Devour never actually opened the market when played for
    /// real (see DevourIntegrationTests.PlayCarrionCrawler_MandatoryMarketDevour_OpensMarketForReal,
    /// which fixed it via a hand-typed card + a directly-mocked IMarketManager/IMarketStateManager).
    /// This exercises the same real-path shape, but with the ACTUAL "carrion_crawler" entry
    /// from cards.json, a REAL MarketManager (via MatchFactory), and every command dispatched
    /// through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class CarrionCrawlerScenarioTests
    {
        [TestMethod]
        public void PlayCarrionCrawler_OpensRealMarketAndDevoursReplacesTheChosenCard()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var carrionCrawler = scenario.GiveCard(PlayerColor.Red, "carrion_crawler");
            var victim = scenario.Context.MarketManager.MarketRow.First();

            scenario.PlayCard(carrionCrawler);

            // The +3 Power should have applied immediately (pushed first, so it resolves
            // before the market-Devour effect, matching card-text order).
            Assert.AreEqual(3, red.Power, "Carrion Crawler should grant +3 Power.");

            // Regression assertion for the real bug this session found: a MANDATORY Devour
            // reached through PlayCardCommand must actually enter market-targeting, not just
            // silently change CurrentState with nothing clickable to advance it.
            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);

            // Market-devour selection isn't a map/site click - it goes through the market
            // selection callback ActionSystem wires when TryStartDevourMarket runs. Simulate
            // it the same way the client's market UI does: call the callback ActionSystem
            // registered via IMarketStateManager.OpenForDevour. Since this harness doesn't
            // attach an IMarketStateManager (headless, no UI), drive it through
            // HandleDevourMarketSelection directly - the same method the real callback invokes.
            var devourCommand = scenario.Context.ActionSystem.HandleDevourMarketSelection(victim);
            Assert.IsNotNull(devourCommand);
            scenario.Dispatch(devourCommand!);

            Assert.DoesNotContain(victim, scenario.Context.MarketManager.MarketRow, "Devoured market card should have left the market row.");
            Assert.Contains(carrionCrawler, scenario.Context.MarketManager.MarketRow, "Carrion Crawler should now occupy the market slot (ReplaceWithSource).");
            Assert.AreEqual(CardLocation.Void, victim.Location);
        }

        [TestMethod]
        public void DevourMarketCommand_ForACardNoLongerInTheMarket_IsRejectedWithNoStateChange()
        {
            // Adversarial scenario: a stale/replayed DevourCardCommand naming a card that has
            // since left the market row (e.g. bought or devoured by someone else) must be
            // rejected by Validate(), not silently no-op-execute against wrong state.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            var carrionCrawler = scenario.GiveCard(PlayerColor.Red, "carrion_crawler");
            scenario.PlayCard(carrionCrawler);
            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);

            var goneCard = CardFactory.CreateNoble(scenario.Context.Random);
            goneCard.Location = CardLocation.Market;
            // Deliberately never added to MarketRow itself - simulates a target that has
            // already left it (bought or devoured by someone else) between the client seeing
            // it and this command arriving.

            var command = new DevourCardCommand(goneCard) { SourceCard = carrionCrawler };
            scenario.AssertRejected(command);

            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState, "Targeting should still be waiting for a real selection.");
        }

        [TestMethod]
        public void PlayCarrionCrawlerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var carrionCrawler = scenario.GiveCard(PlayerColor.Red, "carrion_crawler");

            scenario.DispatchTwice(new PlayCardCommand(carrionCrawler));

            Assert.AreEqual(3, red.Power, "The +3 Power should have applied exactly once, not twice.");
        }
    }
}
