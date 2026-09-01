using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario coverage for DiscardCardCommand (see planning.txt TIER 1 - the 2026-09-01
    /// coverage run flagged it at 56.7% line / 50% branch, added this session alongside
    /// Insane Outcast and never exercised through a real PlayCardCommand -> CommandDispatcher
    /// path). Insane Outcast's own "discard a card from your hand to return Insane Outcast to
    /// the supply" is the only shipped card driving this command's non-Neogi branch.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class InsaneOutcastScenarioTests
    {
        [TestMethod]
        public void PlayInsaneOutcast_DiscardingAHandCard_ReturnsItselfToSupplyNotVoid()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var insaneOutcast = scenario.GiveCard(PlayerColor.Red, "insane_outcast");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(insaneOutcast);

            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState,
                "Insane Outcast's discard is mandatory (not IsOptional) - playing it should enter targeting directly, no popup.");
            Assert.IsEmpty(scenario.Interactions, "Mandatory effects don't raise an accept/decline popup.");

            var command = new DiscardCardCommand(red.Color, noble.Id);
            scenario.Dispatch(command);

            Assert.DoesNotContain(noble, red.Hand, "Noble should have been discarded from hand.");
            Assert.AreEqual(CardLocation.Supply, insaneOutcast.Location,
                "RedirectsToSupplyOnDevourOrPromote should route the self-devour to Supply, not VoidPile.");
            Assert.DoesNotContain(insaneOutcast, scenario.Context.VoidPile, "Insane Outcast must never land in VoidPile.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayInsaneOutcast_WithNoOtherCardInHand_SkipsTheDiscardEntirely()
        {
            // Regression coverage for the top-level HasValidTargets pre-check (see Commit 1 /
            // planning.txt RESOLVED): with nothing else to discard, the mandatory DiscardCard
            // effect has no valid target and no Alternative - it should be skipped cleanly,
            // not leave CurrentState stuck waiting for an impossible click.
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);

            var insaneOutcast = scenario.GiveCard(PlayerColor.Red, "insane_outcast"); // Only card in hand.

            scenario.PlayCard(insaneOutcast);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState,
                "No valid discard target should fall straight through to Normal, not stall.");
            Assert.AreEqual(CardLocation.Played, insaneOutcast.Location, "Insane Outcast stays in Played - it was never actually devoured.");
        }

        [TestMethod]
        public void DiscardCardCommand_NamingACardNotInTheTargetPlayersHand_IsRejectedWithNoStateChange()
        {
            // Adversarial scenario: a forged/stale DiscardCardCommand naming a card that
            // isn't (or is no longer) in the target player's hand must be rejected by
            // Validate(), not silently discard some other card or no-op past CompleteAction().
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var insaneOutcast = scenario.GiveCard(PlayerColor.Red, "insane_outcast");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");
            scenario.PlayCard(insaneOutcast);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            long sequenceBefore = scenario.Context.SequenceNumber;
            var forgedCommand = new DiscardCardCommand(red.Color, "card_that_does_not_exist");
            scenario.Dispatch(forgedCommand);

            Assert.Contains(noble, red.Hand, "The real hand card must be untouched.");
            Assert.AreEqual(sequenceBefore, scenario.Context.SequenceNumber, "A rejected command must not advance SequenceNumber.");
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real discard.");
        }
    }
}
