using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for White Dragon ("Deploy 3 troops. Gain 1 VP for every 2
    /// sites you control.") - the first shipped card using CardEffectProcessor's dynamic-amount
    /// mechanism (CardEffect.DynamicAmountSource/DynamicAmountDivisor, resolved fresh from live
    /// board state instead of a fixed CardEffect.Amount literal) and the first to actually grant
    /// GainResource(TargetResource: VictoryPoints) (previously a completely unhandled branch -
    /// no shipped card used it). Loads the REAL "white_dragon" entry out of the REAL cards.json
    /// and dispatches every command through a REAL CommandDispatcher. Both effects are
    /// automatic/non-targeting, so this card needs no click at all once played - rows 2/3/5/6/8
    /// of the standing test matrix don't apply (no choose-one, no targeting effect at all).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class WhiteDragonScenarioTests
    {
        /// <summary>
        /// Marks the first <paramref name="siteCount"/> sites on the board as owned by Red -
        /// setup only, not going through a command (this codebase has no existing scenario
        /// helper for site control, since no prior card read a site-count at all).
        /// </summary>
        private static Player SetupRedControllingSites(MatchScenario scenario, int siteCount)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            foreach (var site in scenario.Context.MapManager.Sites.Take(siteCount))
            {
                site.Owner = PlayerColor.Red;
            }
            return red;
        }

        // --- Row 1: positive/happy path through real PlayCardCommand -> CommandDispatcher ---

        [TestMethod]
        public void PlayWhiteDragon_Controlling4Sites_GrantsThreeFreeTroopsAndTwoVP()
        {
            var scenario = MatchScenario.Build();
            var red = SetupRedControllingSites(scenario, 4);
            var card = scenario.GiveCard(PlayerColor.Red, "white_dragon");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.PendingFreeTroops, "Deploy 3 troops should credit 3 free deployments - a fixed, non-dynamic amount.");
            Assert.AreEqual(2, red.VictoryPoints, "4 sites controlled / 2 per VP = 2 VP.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Both effects are automatic - no targeting should ever open.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Edge case specific to the dynamic-amount mechanism: integer division must floor,
        // not round. ---

        [TestMethod]
        public void PlayWhiteDragon_ControllingAnOddNumberOfSites_RoundsTheVPDown()
        {
            var scenario = MatchScenario.Build();
            var red = SetupRedControllingSites(scenario, 3); // 3 / 2 must floor to 1, not round to 2.
            var card = scenario.GiveCard(PlayerColor.Red, "white_dragon");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.VictoryPoints, "3 sites / 2 per VP must floor to 1 VP.");
        }

        [TestMethod]
        public void PlayWhiteDragon_ControllingOneSite_GrantsZeroVP()
        {
            var scenario = MatchScenario.Build();
            var red = SetupRedControllingSites(scenario, 1); // 1 / 2 = 0.
            var card = scenario.GiveCard(PlayerColor.Red, "white_dragon");

            scenario.PlayCard(card);

            Assert.AreEqual(0, red.VictoryPoints, "Fewer than 2 controlled sites must grant 0 VP, not round up.");
        }

        [TestMethod]
        public void PlayWhiteDragon_ControllingNoSites_StillGrantsFreeTroopsButZeroVP()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "white_dragon");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.PendingFreeTroops, "The Deploy half is unrelated to site control and must still apply.");
            Assert.AreEqual(0, red.VictoryPoints, "Zero sites controlled is a real, valid amount (0), not a skipped/errored effect.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayWhiteDragonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "white_dragon");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "White Dragon should still be in Blue's hand - the command must not have executed.");
            Assert.AreEqual(0, blue.VictoryPoints);
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayWhiteDragonCommand_DispatchedTwice_SecondDispatchIsRejectedAndDoesNotDoubleGrant()
        {
            var scenario = MatchScenario.Build();
            var red = SetupRedControllingSites(scenario, 4);
            var card = scenario.GiveCard(PlayerColor.Red, "white_dragon");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(3, red.PendingFreeTroops, "Should have applied exactly once, not twice.");
            Assert.AreEqual(2, red.VictoryPoints, "Should have applied exactly once, not twice.");
        }
    }
}
