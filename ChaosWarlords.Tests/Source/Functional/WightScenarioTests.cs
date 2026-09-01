using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// First scenario-harness migration target (see planning.txt TIER 1) - Wight is one of the
    /// two cards a real, session-found bug (broken Choose-one mutual exclusivity) slipped past
    /// while the old WightMechanicsTests.cs stayed green, because that file hand-types the
    /// CardEffect tree and calls command.Execute(context) directly instead of going through
    /// PlayCardCommand -> CommandDispatcher. This file exercises the exact same "accept the
    /// Devour, pick a card, Supplant a troop" sequence, but loads the REAL "wight" entry out of
    /// the REAL cards.json and dispatches every command through a REAL CommandDispatcher - the
    /// old file is NOT deleted, it still pins ActionSystem-internal behavior (stack shape,
    /// interaction-request counts) this file doesn't re-assert.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class WightScenarioTests
    {
        [TestMethod]
        public void PlayWight_AcceptDevourAndSupplant_MutuallyExcludesThePowerAlternative()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var wight = scenario.GiveCard(PlayerColor.Red, "wight");
            var noble = scenario.GiveCard(PlayerColor.Red, "core_noble");

            // A Blue-occupied node Red can reach, so Supplant has a real target.
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var blueTarget = redNode.Neighbors.FirstOrDefault(n => n.Occupant == PlayerColor.None)
                ?? redNode.Neighbors.First();
            blueTarget.Occupant = blue.Color; // Setup only - not going through a command.

            scenario.PlayCard(wight);
            Assert.HasCount(1, scenario.Interactions, "Playing Wight should raise exactly one optional-effect popup.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingDevourHand, scenario.Context.ActionSystem.CurrentState);
            scenario.SelectDevourCard(noble);

            Assert.IsFalse(red.Hand.Contains(noble), "Noble should have been devoured (left the hand).");
            Assert.AreEqual(CardLocation.Void, noble.Location);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(blueTarget, null);

            Assert.AreEqual(red.Color, blueTarget.Occupant, "Supplant should have placed Red's troop.");
            Assert.AreEqual(1, red.TrophyHall, "Supplant's assassinate half should award a trophy.");
            Assert.AreEqual(0, red.Power, "Choose-one mutual exclusivity: accepting Devour->Supplant must NOT also grant the +2 Power Alternative.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        [TestMethod]
        public void PlayWight_DeclineWithNoOtherCardsInHand_GrantsThePowerAlternativeInstead()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var wight = scenario.GiveCard(PlayerColor.Red, "wight"); // Only card in hand.

            scenario.PlayCard(wight);

            Assert.IsEmpty(scenario.Interactions, "No valid Devour target (empty hand) means no popup - the Alternative fires directly.");
            Assert.AreEqual(2, red.Power, "Empty-hand fallback should still grant the +2 Power Alternative.");
        }

        [TestMethod]
        public void PlayWight_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            // Adversarial scenario (see planning.txt section 2's testing policy): a command
            // referencing a card runtime id that ISN'T in the currently-active player's hand
            // must be rejected by Validate(), with no state mutated at all - this is the exact
            // shape an untrusted client sending a forged command would produce.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var wight = scenario.GiveCard(PlayerColor.Blue, "wight"); // Belongs to Blue, not the active player.
            long sequenceBefore = scenario.Context.SequenceNumber;

            scenario.PlayCard(wight);

            Assert.IsTrue(blue.Hand.Contains(wight), "Wight should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, red.Power, "No effect should have resolved for anyone.");
            Assert.AreEqual(sequenceBefore, scenario.Context.SequenceNumber, "A rejected command must not advance SequenceNumber.");
        }
    }
}
