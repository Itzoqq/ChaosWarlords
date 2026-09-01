using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Skeletal Horde (see planning.txt TIER 1 item 5). Before
    /// hardening tests for it, cross-checked the real card image (extracted_og_cards/
    /// extracted_cards_abberations_undead/card_31_row5_col3.jpg, 2026-09-01) since planning.txt
    /// flagged its shape as structurally identical to Wight's shipped "Choose one" bug -
    /// turned out NOT to be the same bug: the real card has NO "Choose one:" language anywhere,
    /// just "Deploy 2 troops." (unconditional) followed by a separate "Devour this card ->
    /// Deploy 3 troops" (optional, additive bonus, not a mutually-exclusive alternative) -
    /// exactly what the shipped JSON already implements (GainResource(2) unconditional +
    /// optional Devour(Self)->GainResource(3), no Alternative). The cross-check DID find two
    /// real data-value bugs, fixed alongside this file: Cost was 3, real card is 2;
    /// InnerCircleVP was 3, real card is 2 (DeckVP 1 and Aspect Oblivion - the project's own
    /// devour-flavored-Aspect convention, not the real "Conquest" icon - were both already
    /// correct).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SkeletalHordeScenarioTests
    {
        [TestMethod]
        public void PlaySkeletalHorde_AcceptDevour_GetsBothDeploysStacked()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var skeletalHorde = scenario.GiveCard(PlayerColor.Red, "skeletal_horde");

            scenario.PlayCard(skeletalHorde);

            Assert.AreEqual(2, red.PendingFreeTroops, "The base 'Deploy 2 troops' should apply immediately and unconditionally.");
            Assert.HasCount(1, scenario.Interactions, "Self-devour is always a valid target (the card itself), so the popup should be requested.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(5, red.PendingFreeTroops, "Accepting the devour should ADD 3 more troops on top of the base 2 - not replace it (no Alternative, both effects stack).");
            Assert.AreEqual(CardLocation.Played, skeletalHorde.Location, "Self-devour stays 'Played' until end of turn (deferred, same as Cultist of Myrkul's pattern).");
            Assert.Contains(skeletalHorde, scenario.Context.CardsMarkedForTurnEndDevour);
        }

        [TestMethod]
        public void PlaySkeletalHorde_DeclineDevour_KeepsOnlyTheBaseDeploy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var skeletalHorde = scenario.GiveCard(PlayerColor.Red, "skeletal_horde");

            scenario.PlayCard(skeletalHorde);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(2, red.PendingFreeTroops, "Declining should leave just the base 'Deploy 2 troops' - no bonus, but not less either (no Alternative to fall back to).");
            Assert.AreEqual(CardLocation.Played, skeletalHorde.Location, "Declined - never devoured at all.");
            Assert.DoesNotContain(skeletalHorde, scenario.Context.CardsMarkedForTurnEndDevour);
        }

        [TestMethod]
        public void PlaySkeletalHorde_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var skeletalHorde = scenario.GiveCard(PlayerColor.Blue, "skeletal_horde");

            scenario.AssertRejected(new PlayCardCommand(skeletalHorde));

            Assert.Contains(skeletalHorde, blue.Hand, "Skeletal Horde should still be in Blue's hand - the command must not have executed.");
        }

        [TestMethod]
        public void PlaySkeletalHordeCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // TIER 1 matrix row 7 (double-dispatch/replay - see planning.txt section 6.C.2).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var skeletalHorde = scenario.GiveCard(PlayerColor.Red, "skeletal_horde");

            scenario.DispatchTwice(new PlayCardCommand(skeletalHorde));

            Assert.AreEqual(2, red.PendingFreeTroops, "The base deploy should have applied exactly once, not twice.");
            Assert.HasCount(1, scenario.Interactions, "The optional-effect popup should have been raised exactly once, not twice.");
        }
    }
}
