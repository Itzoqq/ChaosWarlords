using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Air Elemental Myrmidon ("Place a spy. At end of turn,
    /// promote an Obedience card played this turn.") - the first shipped card using the new
    /// CardEffect.RequiredPromotionAspect primitive (planning.txt TIER 1 item 7: aspect-filtered
    /// promote). Extends the pre-existing EffectType.Promote deferred-credit flow (core_noble,
    /// Cultist of Myrkul) with an optional aspect filter on TurnContext's PromotionCredit -
    /// TurnContextTests.cs/PromoteInputModeTests.cs already cover the filter mechanism itself in
    /// isolation; this proves it end-to-end through the REAL "air_elemental_myrmidon" cards.json
    /// entry and a REAL CommandDispatcher. "Obedience" is this codebase's CardAspect.Order (see
    /// the tyrants-rules skill's rename table).
    ///
    /// The actual credit REDEMPTION (clicking which played card to promote) is mediated by
    /// PromoteInputMode, a client-side (non-Core) class never exercised by MatchScenario -
    /// matching CultistOfMyrkulScenarioTests.cs's own precedent, this drives that step manually
    /// (TurnContext.HasValidCreditFor/ConsumeCreditFor + a real PromoteCommand dispatch),
    /// mirroring exactly what PromoteInputMode.HandleLeftClick does.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class AirElementalMyrmidonScenarioTests
    {
        [TestMethod]
        public void PlayAirElementalMyrmidon_PlacesASpy_AndBanksAnObedienceFilteredCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "air_elemental_myrmidon");
            var site = scenario.Context.MapManager.Sites.First();

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Banking a credit is non-targeting/automatic - no popup should be pending after the spy is placed.");
        }

        [TestMethod]
        public void EndOfTurnRedemption_ObedienceCardPlayedThisTurn_CanBePromoted_ANonObedienceCardCannot()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "air_elemental_myrmidon");
            var site = scenario.Context.MapManager.Sites.First();
            // "test_guard" is Aspect.Order (Obedience) - a real eligible target.
            var orderCard = scenario.GiveCard(PlayerColor.Red, "test_guard");
            scenario.PlayCard(orderCard);
            // core_house_guard is Aspect.Warlord (Conquest) - NOT eligible for this filtered credit.
            var nonOrderCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            scenario.PlayCard(nonOrderCard);

            scenario.PlayCard(card);
            scenario.ClickTarget(null, site);

            var context = scenario.Context.TurnManager.CurrentTurnContext;
            Assert.IsFalse(context.HasValidCreditFor(nonOrderCard), "A Warlord-aspect card must not be a valid target for an Order-filtered credit.");
            Assert.IsTrue(context.HasValidCreditFor(orderCard), "An Order-aspect card must be a valid target.");

            context.ConsumeCreditFor(orderCard);
            scenario.Dispatch(new PromoteCommand(orderCard));

            Assert.Contains(orderCard, red.InnerCircle);
            Assert.DoesNotContain(orderCard, red.PlayedCards);
            Assert.AreEqual(0, context.PendingPromotionsCount);

            // The redemption is now fully resolved (ActionSystem.IsTargeting() is false, since
            // banking/consuming a credit is non-targeting) - ending the turn normally must
            // succeed, proving EndTurnCommand.Validate() doesn't reject it.
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreNotEqual(red, scenario.Context.ActivePlayer, "The turn should have actually ended and rotated away from Red.");
        }

        [TestMethod]
        public void PlayAirElementalMyrmidonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "air_elemental_myrmidon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayAirElementalMyrmidonCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "air_elemental_myrmidon");

            scenario.DispatchTwice(new PlayCardCommand(card));

            // PlaceSpy is still pending a site click (blocks the execution stack) - the Promote
            // effect underneath it hasn't resolved yet, so no credit should be banked at all.
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Should still be waiting for exactly the one site click the first play triggered.");
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The Promote effect must not have resolved before PlaceSpy did.");

            var site = scenario.Context.MapManager.Sites.First();
            scenario.ClickTarget(null, site);

            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The credit must have been banked exactly once, not twice, once the spy is actually placed.");
            Assert.HasCount(1, site.Spies, "The spy must have been placed exactly once, not twice.");
        }
    }
}
