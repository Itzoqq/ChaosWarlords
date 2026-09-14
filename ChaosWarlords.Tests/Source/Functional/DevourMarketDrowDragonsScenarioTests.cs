using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for planning.txt TIER 1 item 8's transcribed cards pairing a
    /// mandatory effect with a plain "you may devour a card in the market" bonus - the same
    /// standalone-optional-Devour(Market) shape Market Corruptor already exercises, but here as
    /// a SECOND, independent top-level effect rather than the card's only effect: Cult Fanatic
    /// ("Gain 2 Influence. You may devour a card in the market.") and White Wyrmling ("Deploy 2
    /// troops. You may devour a card in the market."). Loads the REAL cards.json entries and
    /// dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DevourMarketDrowDragonsScenarioTests
    {
        // --- Cult Fanatic: accept the optional Devour. ---

        [TestMethod]
        public void PlayCultFanatic_AcceptDevour_GrantsInfluenceAndDevoursTheChosenMarketCard()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "cult_fanatic");
            var victim = scenario.Context.MarketManager.MarketRow.First();

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Influence, "The mandatory GainResource must apply regardless of the optional Devour's outcome.");
            Assert.HasCount(1, scenario.Interactions, "A real market target exists, so the optional-effect popup should fire.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);
            var devourCommand = scenario.Context.ActionSystem.HandleDevourMarketSelection(victim);
            Assert.IsNotNull(devourCommand);
            scenario.Dispatch(devourCommand!);

            Assert.DoesNotContain(victim, scenario.Context.MarketManager.MarketRow);
            Assert.AreEqual(CardLocation.Void, victim.Location);
        }

        // --- Cult Fanatic: decline the optional Devour. ---

        [TestMethod]
        public void PlayCultFanatic_DeclineDevour_StillGrantsInfluenceButDevoursNothing()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "cult_fanatic");
            int marketCountBefore = scenario.Context.MarketManager.MarketRow.Count;

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(2, red.Influence);
            Assert.HasCount(marketCountBefore, scenario.Context.MarketManager.MarketRow, "Declining must leave the market untouched.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayCultFanatic_WithEmptyMarket_SkipsThePopupButStillGrantsInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "cult_fanatic");
            scenario.Context.MarketManager.MarketRow.Clear();

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Influence);
            Assert.IsEmpty(scenario.Interactions, "No valid market target means no popup - the HasValidTargets pre-check should skip it cleanly.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayCultFanaticCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "cult_fanatic");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void DevourCardCommand_ForACardNoLongerInTheMarket_IsRejectedWhileCultFanaticEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "cult_fanatic");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);

            var goneCard = CardFactory.CreateNoble(scenario.Context.Random);
            goneCard.Location = CardLocation.Market;

            var command = new DevourCardCommand(goneCard) { SourceCard = card };
            scenario.AssertRejected(command);

            Assert.AreEqual(ActionState.TargetingDevourMarket, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayCultFanaticCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "cult_fanatic");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.Influence, "Should have applied exactly once, not twice.");
            Assert.HasCount(1, scenario.Interactions, "The optional-effect popup should have been raised exactly once, not twice.");
        }

        // --- White Wyrmling: accept the optional Devour. ---

        [TestMethod]
        public void PlayWhiteWyrmling_AcceptDevour_CreditsFreeTroopsAndDevoursTheChosenMarketCard()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "white_wyrmling");
            var victim = scenario.Context.MarketManager.MarketRow.First();

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.PendingFreeTroops);
            scenario.RespondToLatestInteraction(accept: true);
            var devourCommand = scenario.Context.ActionSystem.HandleDevourMarketSelection(victim);
            Assert.IsNotNull(devourCommand);
            scenario.Dispatch(devourCommand!);

            Assert.DoesNotContain(victim, scenario.Context.MarketManager.MarketRow);
        }

        [TestMethod]
        public void PlayWhiteWyrmling_DeclineDevour_StillCreditsFreeTroopsButDevoursNothing()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "white_wyrmling");
            int marketCountBefore = scenario.Context.MarketManager.MarketRow.Count;

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(2, red.PendingFreeTroops);
            Assert.HasCount(marketCountBefore, scenario.Context.MarketManager.MarketRow);
        }

        [TestMethod]
        public void PlayWhiteWyrmlingCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "white_wyrmling");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayWhiteWyrmlingCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "white_wyrmling");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.PendingFreeTroops, "Should have applied exactly once, not twice.");
            Assert.HasCount(1, scenario.Interactions, "The optional-effect popup should have been raised exactly once, not twice.");
        }
    }
}
