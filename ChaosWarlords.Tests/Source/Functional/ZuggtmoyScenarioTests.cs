using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Zuggtmoy (planning.txt TIER 1 #1's "Zuggtmoy" bullet) -
    /// "Devour a card in your inner circle -> Gain 3 Influence and, at end of turn, promote up
    /// to 2 other cards played this turn." This is the first shipped card whose OnSuccess chain
    /// is TWO levels deep through a non-targeting middle node (Devour -> GainResource ->
    /// Promote) - see CardEffectProcessor.PushSuccessorEffect's doc comment for why that shape
    /// needs deliberate, single-path propagation. These tests are both Zuggtmoy's own card
    /// coverage and the regression pin for that propagation behavior (PendingPromotionsCount
    /// must land on exactly 2, not double-count the chain's second level).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ZuggtmoyScenarioTests
    {
        [TestMethod]
        public void PlayZuggtmoy_AcceptDevour_DevoursInnerCircleCardGainsInfluenceAndBanksTwoPromotionCredits()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var victim = scenario.PutCardInInnerCircle(PlayerColor.Red, "core_noble");

            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");
            scenario.PlayCard(zuggtmoy);

            Assert.HasCount(1, scenario.Interactions, "A non-empty inner circle should raise the optional Devour confirmation.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingDevourInnerCircle, scenario.Context.ActionSystem.CurrentState);

            var command = scenario.SelectInnerCircleDevourCard(victim);

            Assert.IsNotNull(command, "Selecting a real inner-circle card should produce a DevourCardCommand.");
            Assert.DoesNotContain(victim, red.InnerCircle.ToList(), "The devoured card should be removed from the inner circle.");
            Assert.AreEqual(CardLocation.Void, victim.Location);
            Assert.AreEqual(3, red.Influence, "Should have gained exactly 3 Influence, not double.");
            Assert.AreEqual(2, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount,
                "Should have banked exactly 2 Promote credits (\"up to 2\"), not double-fired to 4 - see CardEffectProcessor.PushSuccessorEffect.");
            Assert.IsTrue(scenario.Context.TurnManager.CurrentTurnContext.CanDeclineRemainingPromotions,
                "\"Promote up to 2\" credits must be voluntarily declinable.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayZuggtmoy_DeclineDevour_GrantsNothing()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var victim = scenario.PutCardInInnerCircle(PlayerColor.Red, "core_noble");

            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");
            scenario.PlayCard(zuggtmoy);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.Contains(victim, red.InnerCircle.ToList(), "Declining must not devour the inner circle card.");
            Assert.AreEqual(0, red.Influence, "Declining must not grant any Influence.");
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "Declining must not bank a promotion credit.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayZuggtmoy_WithEmptyInnerCircle_SkipsEntirelyWithNoInteractionRequested()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            Assert.IsEmpty(red.InnerCircle, "Setup check: Red's inner circle must be empty for this test.");

            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");
            scenario.PlayCard(zuggtmoy);

            Assert.IsEmpty(scenario.Interactions, "No valid Devour target exists, so the optional-effect popup must never be raised.");
            Assert.AreEqual(0, red.Influence);
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayZuggtmoy_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var zuggtmoy = scenario.GiveCard(PlayerColor.Blue, "zuggtmoy"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(zuggtmoy));

            Assert.Contains(zuggtmoy, blue.Hand, "Zuggtmoy should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions, "No popup should have been raised for a rejected command.");
            Assert.AreEqual(0, red.Influence);
        }

        [TestMethod]
        public void SelectInnerCircleDevourCard_TargetingCardNotInInnerCircle_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            scenario.PutCardInInnerCircle(PlayerColor.Red, "core_noble");
            var handCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard"); // A real card, but NOT in the inner circle.

            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");
            scenario.PlayCard(zuggtmoy);
            scenario.RespondToLatestInteraction(accept: true);

            long sequenceBefore = scenario.Context.SequenceNumber;
            string hashBefore = scenario.Context.GetStateHash();

            var command = scenario.SelectInnerCircleDevourCard(handCard);

            Assert.IsNull(command, "A hand card is not a valid Inner Circle devour target - no command should be produced.");
            Assert.AreEqual(sequenceBefore, scenario.Context.SequenceNumber, "Nothing should have been dispatched.");
            Assert.AreEqual(hashBefore, scenario.Context.GetStateHash(), "State must be unchanged by a rejected selection.");
            Assert.AreEqual(ActionState.TargetingDevourInnerCircle, scenario.Context.ActionSystem.CurrentState, "Targeting should still be in progress, waiting for a valid target.");
        }

        [TestMethod]
        public void PlayZuggtmoyCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 2's
            // STANDING TEST MATRIX).
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            scenario.PutCardInInnerCircle(PlayerColor.Red, "core_noble");
            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");

            scenario.DispatchTwice(new PlayCardCommand(zuggtmoy));

            Assert.HasCount(1, scenario.Interactions, "The optional Devour popup should have been raised exactly once, not twice.");
        }

        [TestMethod]
        public void DevourCardCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var victim = scenario.PutCardInInnerCircle(PlayerColor.Red, "core_noble");
            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");
            scenario.PlayCard(zuggtmoy);
            scenario.RespondToLatestInteraction(accept: true);

            var command = scenario.SelectInnerCircleDevourCard(victim);
            Assert.IsNotNull(command);

            scenario.AssertRejected(command!, "The same DevourCardCommand replayed after the card has already been devoured must be rejected.");

            Assert.AreEqual(3, red.Influence, "Influence must still reflect exactly one application, not a second one from the replay.");
            Assert.AreEqual(2, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "Promotion credits must still reflect exactly one application.");
        }

        [TestMethod]
        public void DevourCardCommand_DtoRoundTrip_StillAppliesInfluenceAndPromotionCredits()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var victim = scenario.PutCardInInnerCircle(PlayerColor.Red, "core_noble");
            var zuggtmoy = scenario.GiveCard(PlayerColor.Red, "zuggtmoy");
            scenario.PlayCard(zuggtmoy);
            scenario.RespondToLatestInteraction(accept: true);

            var command = scenario.Context.ActionSystem.HandleDevourInnerCircleSelection(victim);
            Assert.IsNotNull(command, "Setup check: selecting a real inner-circle card should produce a real DevourCardCommand.");

            var dto = command!.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as DevourCardCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a DevourCardCommand.");
            Assert.AreEqual(command.CardRuntimeId, hydrated!.CardRuntimeId);

            scenario.Dispatch(hydrated);

            Assert.DoesNotContain(victim, red.InnerCircle.ToList());
            Assert.AreEqual(CardLocation.Void, victim.Location);
            Assert.AreEqual(3, red.Influence);
            Assert.AreEqual(2, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }
    }
}
