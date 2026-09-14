using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Dragonclaw ("Assassinate a troop. Then, if you have 5 or
    /// more player troops in your trophy hall, gain 2 Power.") - planning.txt TIER 1 item 8. The
    /// first shipped card using the new ConditionType.PlayerTrophyHallCount (the gating
    /// counterpart to DynamicAmountSource.PlayerTrophyHallCount - sums Player.TrophyHallByColor
    /// EXCLUDING Neutral/None, unlike Revenant's ConditionType.TrophyHallCount which counts ANY
    /// color). Loads the REAL "dragonclaw" entry out of the REAL cards.json and dispatches every
    /// command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DragonclawScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithOneAdjacentEnemyTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = PlayerColor.Blue;
            return (red, target);
        }

        // --- Row 1: positive/happy path, condition MET ---

        [TestMethod]
        public void PlayDragonclaw_FivePlayerTroopsAlreadyInTrophyHall_AssassinatesAndGrantsTwoPower()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            red.SetTrophyHall(5, PlayerColor.Blue); // "Player" troops - not Neutral.
            var card = scenario.GiveCard(PlayerColor.Red, "dragonclaw");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(6, red.TrophyHall, "The 5 pre-existing + this card's own assassination.");
            Assert.AreEqual(2, red.Power, "5+ player troops in trophy hall meets the threshold - the gated Power gain should fire.");
        }

        // --- Row 1b: positive/happy path, condition NOT met (below threshold) ---

        [TestMethod]
        public void PlayDragonclaw_ThreePlayerTroopsInTrophyHall_AssassinatesButGrantsNoPower()
        {
            // 3 pre-existing + this card's own assassination reaches only 4 - still below the 5
            // threshold (unlike the "5 pre-existing" happy-path test, where the card's own kill
            // isn't needed to cross it at all).
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            red.SetTrophyHall(3, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "dragonclaw");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(4, red.TrophyHall);
            Assert.AreEqual(0, red.Power, "Below the threshold - the gated Power gain must not fire.");
        }

        // --- The Neutral-exclusion distinction from Revenant's ConditionType.TrophyHallCount ---

        [TestMethod]
        public void PlayDragonclaw_FiveNeutralTroopsButNoPlayerTroopsInTrophyHall_GrantsNoPower()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            red.SetTrophyHall(5, PlayerColor.Neutral); // Would meet Revenant's "any color" threshold, but NOT Dragonclaw's.
            var card = scenario.GiveCard(PlayerColor.Red, "dragonclaw");

            scenario.PlayCard(card);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(0, red.Power, "Neutral/white troops must not count toward Dragonclaw's PLAYER-troop-only threshold.");
        }

        // --- Row 3: no-valid-target fallback ---

        [TestMethod]
        public void PlayDragonclaw_NoTroopsAnywhere_SkipsAssassinateAndNeverGrantsPower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.SetTrophyHall(20, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "dragonclaw");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(0, red.Power, "No Assassinate ever happened, so its OnSuccess chain (the conditional Power gain) must never have run.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayDragonclawCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "dragonclaw");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        // --- Row 5: stale/nonexistent target ---

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileDragonclawEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "dragonclaw");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new AssassinateCommand(999999, card.Id), "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayDragonclawCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            red.SetTrophyHall(5, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "dragonclaw");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new AssassinateCommand(target.Id, card.Id));

            Assert.AreEqual(6, red.TrophyHall, "Should have applied exactly once, not twice.");
            Assert.AreEqual(2, red.Power, "Should have applied exactly once, not twice.");
        }
    }
}
