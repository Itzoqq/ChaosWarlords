using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for the plain, non-targeting GainResource cards among
    /// planning.txt TIER 1 item 8's transcribed Drow/Dragons cards: Bounty Hunter ("Gain 3
    /// Power"), Severin Silrajin ("Gain 5 Power"), Mercenary Squad ("Deploy 3 troops" -
    /// GainResource(Troops), same automatic/non-targeting shape as White Dragon's Deploy half),
    /// Red Wyrmling ("Gain 2 Power. Gain 2 Influence." - two independent mandatory
    /// GainResource effects), and Dragon Cultist ("Choose one: Gain 2 Power. Or, gain 2
    /// Influence." - a GainResource/GainResource Alternative pair, the first shipped card where
    /// BOTH branches of a choose-one are the same effect type with a different resource).
    /// Each card is a single-step, non-targeting play - rows 2/3/5/6/8 of the standing matrix
    /// don't apply except where noted (Dragon Cultist's choose-one exercises row 2 directly).
    /// Loads the REAL cards.json entries and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SimpleGainResourceDrowDragonsScenarioTests
    {
        // --- Bounty Hunter: "Gain 3 Power". ---

        [TestMethod]
        public void PlayBountyHunter_GrantsThreePower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "bounty_hunter");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.Power);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayBountyHunterCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "bounty_hunter");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayBountyHunterCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "bounty_hunter");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(3, red.Power, "Should have applied exactly once, not twice.");
        }

        // --- Severin Silrajin: "Gain 5 Power". ---

        [TestMethod]
        public void PlaySeverinSilrajin_GrantsFivePower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "severin_silrajin");

            scenario.PlayCard(card);

            Assert.AreEqual(5, red.Power);
        }

        [TestMethod]
        public void PlaySeverinSilrajinCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "severin_silrajin");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlaySeverinSilrajinCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "severin_silrajin");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(5, red.Power, "Should have applied exactly once, not twice.");
        }

        // --- Mercenary Squad: "Deploy 3 troops" (GainResource(Troops), automatic/non-targeting). ---

        [TestMethod]
        public void PlayMercenarySquad_CreditsThreeFreeDeployments()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "mercenary_squad");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.PendingFreeTroops);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "GainResource(Troops) credits a deployment - it does not itself open targeting.");
        }

        [TestMethod]
        public void PlayMercenarySquadCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "mercenary_squad");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayMercenarySquadCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "mercenary_squad");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(3, red.PendingFreeTroops, "Should have applied exactly once, not twice.");
        }

        // --- Red Wyrmling: "Gain 2 Power. Gain 2 Influence." - two independent mandatory effects. ---

        [TestMethod]
        public void PlayRedWyrmling_GrantsBothPowerAndInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "red_wyrmling");

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Power);
            Assert.AreEqual(2, red.Influence);
        }

        [TestMethod]
        public void PlayRedWyrmlingCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "red_wyrmling");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayRedWyrmlingCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "red_wyrmling");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.Power, "Should have applied exactly once, not twice.");
            Assert.AreEqual(2, red.Influence, "Should have applied exactly once, not twice.");
        }

        // --- Dragon Cultist: "Choose one: Gain 2 Power. Or, gain 2 Influence." ---

        [TestMethod]
        public void PlayDragonCultist_AcceptFirstBranch_GrantsPowerOnly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "dragon_cultist");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Gain 2 Power vs. gain 2 Influence.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(2, red.Power);
            Assert.AreEqual(0, red.Influence, "The declined branch must not also apply.");
        }

        [TestMethod]
        public void PlayDragonCultist_DeclineFirstBranch_GrantsInfluenceOnly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "dragon_cultist");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(0, red.Power, "The declined branch must not also apply.");
            Assert.AreEqual(2, red.Influence);
        }

        [TestMethod]
        public void PlayDragonCultistCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "dragon_cultist");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.IsEmpty(scenario.Interactions);
        }

        [TestMethod]
        public void PlayDragonCultistCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "dragon_cultist");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }
    }
}
