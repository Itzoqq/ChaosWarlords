using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Market Corruptor (see planning.txt TIER 1 item 5 - the
    /// matrix pass on the two cards not yet migrated to MatchScenario, alongside Skeletal
    /// Horde). "You may devour a card from the Market to gain 3 Influence" - unlike Wight/
    /// Cultist of Myrkul, this is a plain optional "if you do X, get Y" with no unconditional
    /// baseline and no Alternative fallback; declining grants nothing at all. The card text
    /// itself has no "Choose one:"/"Or," ambiguity, so (unlike Skeletal Horde) this one didn't
    /// need a fresh image cross-check before hardening - the shape already matches what's
    /// printed with no missing "or" clause to miss.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MarketCorruptorScenarioTests
    {
        [TestMethod]
        public void PlayMarketCorruptor_AcceptDevour_GainsInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var corruptor = scenario.GiveCard(PlayerColor.Red, "market_corruptor");
            var victim = scenario.Context.MarketManager.MarketRow.First();

            scenario.PlayCard(corruptor);

            Assert.HasCount(1, scenario.Interactions, "A real market target exists, so the optional-effect popup should fire.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);
            var devourCommand = scenario.Context.ActionSystem.HandleDevourMarketSelection(victim);
            Assert.IsNotNull(devourCommand);
            scenario.Dispatch(devourCommand!);

            Assert.AreEqual(3, red.Influence, "Accepting should grant +3 Influence.");
            Assert.DoesNotContain(victim, scenario.Context.MarketManager.MarketRow);
            Assert.AreEqual(CardLocation.Void, victim.Location);
        }

        [TestMethod]
        public void PlayMarketCorruptor_DeclineDevour_GrantsNothing()
        {
            // Unlike Wight/Cultist of Myrkul, there's no Alternative here - the real card is a
            // plain "you may", not a "choose one". Declining should leave the player with
            // nothing at all, not some fallback resource.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var corruptor = scenario.GiveCard(PlayerColor.Red, "market_corruptor");

            scenario.PlayCard(corruptor);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(0, red.Influence, "Declining a plain optional effect (no Alternative) must grant nothing.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayMarketCorruptor_WithEmptyMarket_SkipsThePopupEntirely()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var corruptor = scenario.GiveCard(PlayerColor.Red, "market_corruptor");
            scenario.Context.MarketManager.MarketRow.Clear(); // No valid Devour target anywhere.

            scenario.PlayCard(corruptor);

            Assert.IsEmpty(scenario.Interactions, "No valid market target means no popup - the HasValidTargets pre-check should skip it cleanly.");
            Assert.AreEqual(0, red.Influence);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Should fall straight through to Normal, not stall waiting for an impossible click.");
        }

        [TestMethod]
        public void PlayMarketCorruptor_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var corruptor = scenario.GiveCard(PlayerColor.Blue, "market_corruptor");

            scenario.AssertRejected(new PlayCardCommand(corruptor));

            Assert.Contains(corruptor, blue.Hand, "Market Corruptor should still be in Blue's hand - the command must not have executed.");
        }

        [TestMethod]
        public void DevourMarketCommand_ForACardNoLongerInTheMarket_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var corruptor = scenario.GiveCard(PlayerColor.Red, "market_corruptor");

            scenario.PlayCard(corruptor);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);

            var goneCard = CardFactory.CreateNoble(scenario.Context.Random);
            goneCard.Location = CardLocation.Market;
            // Never added to MarketRow - simulates a target that has already left it.

            var command = new DevourCardCommand(goneCard) { SourceCard = corruptor };
            scenario.AssertRejected(command);

            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real selection.");
        }

        [TestMethod]
        public void PlayMarketCorruptorCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var corruptor = scenario.GiveCard(PlayerColor.Red, "market_corruptor");

            scenario.DispatchTwice(new PlayCardCommand(corruptor));

            Assert.HasCount(1, scenario.Interactions, "The optional-effect popup should have been raised exactly once, not twice.");
        }
    }
}
