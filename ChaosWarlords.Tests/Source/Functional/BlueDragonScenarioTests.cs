using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Blue Dragon ("At end of turn, promote up to 2 other
    /// cards played this turn, then gain 1 VP for every 3 cards in your inner circle.") - the
    /// deferred end-of-turn promotion-credit shape (Cultist of Myrkul/Zuggtmoy's
    /// PromotionCreditIsOptional "up to N") combined for the first time with a trailing effect
    /// (CardEffect.PromotionCompletionEffect) that must be computed AFTER the deferred
    /// redemption actually happens, not when the card is played (see planning.txt's BLUE DRAGON
    /// WARNING). Deliberately NOT modeled as this node's own OnSuccess - see
    /// CardEffect.PromotionCompletionEffect's doc comment for why that would fire at the wrong
    /// time. Loads the REAL "blue_dragon" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher; MatchManager.EndTurn (driven by the always-
    /// recorded, always-replayed EndTurnCommand) is the actual firing point under test.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class BlueDragonScenarioTests
    {
        private static void SeedInnerCircle(MatchScenario scenario, PlayerColor color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                scenario.PutCardInInnerCircle(color, "core_house_guard");
            }
        }

        // --- Row 1: happy path - the completion effect must count AFTER the promotion ---

        [TestMethod]
        public void PlayBlueDragon_PromoteOneOtherCardThenEndTurn_GainsVpForInnerCircleCountAfterPromotion()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SeedInnerCircle(scenario, PlayerColor.Red, 2); // Pre-existing: 2 cards.
            int vpBefore = red.VictoryPoints;

            var guard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.PlayCard(guard); // In Played pile now - a valid "other card" promote target.

            var dragon = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            scenario.PlayCard(dragon);

            Assert.AreEqual(2, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "\"Up to 2\" should bank 2 credits.");
            Assert.IsTrue(scenario.Context.TurnManager.CurrentTurnContext.CanDeclineRemainingPromotions, "\"Up to 2\" credits must be voluntarily declinable.");

            scenario.Dispatch(new PromoteCommand(guard));
            Assert.Contains(guard, red.InnerCircle, "PromoteCommand should have moved the guard into the inner circle immediately.");

            scenario.Dispatch(new EndTurnCommand());

            // Inner circle after promotion: 2 pre-existing + 1 promoted guard = 3 -> floor(3/3) = 1 VP.
            // If the amount were (wrongly) computed BEFORE the promotion, this would be floor(2/3) = 0.
            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "1 VP for every 3 inner-circle cards, counted AFTER this turn's promotion(s).");
        }

        // --- Decline path: the trailing effect must still fire even with 0 promotions ---

        [TestMethod]
        public void PlayBlueDragon_DeclineBothPromotionsThenEndTurn_StillGainsVpForExistingInnerCircleCount()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SeedInnerCircle(scenario, PlayerColor.Red, 6); // floor(6/3) = 2 VP.
            int vpBefore = red.VictoryPoints;

            var dragon = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            scenario.PlayCard(dragon);

            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(vpBefore + 2, red.VictoryPoints, "Declining (or having no valid target for) the promotion(s) must NOT skip the trailing VP gain.");
        }

        [TestMethod]
        public void PlayBlueDragon_NoInnerCircleCardsAtAll_EndTurnGrantsZeroVpWithoutError()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int vpBefore = red.VictoryPoints;

            var dragon = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            scenario.PlayCard(dragon);
            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(vpBefore, red.VictoryPoints, "An empty inner circle must resolve to 0 VP, not throw or grant a fallback amount.");
        }

        // --- The completion effect must only ever fire once, exactly at the turn it registered on ---

        [TestMethod]
        public void PlayBlueDragon_EndTurnTwiceAcrossTurns_OnlyGrantsTheVpOnce()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SeedInnerCircle(scenario, PlayerColor.Red, 3);
            int vpBefore = red.VictoryPoints;

            var dragon = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            scenario.PlayCard(dragon);
            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "Setup check: the completion effect fired once at the end of Red's turn.");

            scenario.AsActivePlayer(PlayerColor.Red); // Cycle back around to Red without playing anything else.
            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "A later, unrelated EndTurn must not re-fire the already-drained completion effect.");
        }

        // --- Two independent sources registering the same turn ---

        [TestMethod]
        public void PlayTwoBlueDragonsSameTurn_BothIndependentlyGrantVpAtEndOfTurn()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SeedInnerCircle(scenario, PlayerColor.Red, 3); // floor(3/3) = 1 VP per registered completion.
            int vpBefore = red.VictoryPoints;

            var dragon1 = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            var dragon2 = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            scenario.PlayCard(dragon1);
            scenario.PlayCard(dragon2);

            Assert.AreEqual(4, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "2 credits from each of the 2 copies.");

            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(vpBefore + 2, red.VictoryPoints, "Both copies' completion effects must fire independently - 1 VP each, not deduplicated.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayBlueDragonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var dragon = scenario.GiveCard(PlayerColor.Blue, "blue_dragon");

            scenario.AssertRejected(new PlayCardCommand(dragon));

            Assert.Contains(dragon, blue.Hand, "Blue Dragon should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        // --- Row 5/7: stale/already-consumed target, double-dispatch ---

        [TestMethod]
        public void PromoteCommand_DispatchedTwiceForTheSameCardDuringRedemption_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var guard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.PlayCard(guard);
            var dragon = scenario.GiveCard(PlayerColor.Red, "blue_dragon");
            scenario.PlayCard(dragon);

            scenario.DispatchTwice(new PromoteCommand(guard));

            Assert.AreEqual(1, red.InnerCircle.Count(c => c == guard), "The guard must have been promoted exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void PlayCardCommand_DtoRoundTrip_StillPlaysBlueDragonAndTheCompletionEffectStillFires()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            SeedInnerCircle(scenario, PlayerColor.Red, 3);
            int vpBefore = red.VictoryPoints;
            var dragon = scenario.GiveCard(PlayerColor.Red, "blue_dragon");

            var command = new PlayCardCommand(dragon);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as PlayCardCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a PlayCardCommand.");

            scenario.Dispatch(hydrated);
            Assert.AreEqual(2, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);

            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(vpBefore + 1, red.VictoryPoints, "The completion effect must still fire through a hydrated-and-dispatched replay of the play itself.");
        }
    }
}
