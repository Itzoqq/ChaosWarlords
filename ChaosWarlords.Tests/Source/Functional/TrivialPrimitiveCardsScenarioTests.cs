using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Lighter scenario-harness matrix pass (planning.txt TIER 1 item 6) across the 8 shipped
    /// cards with no card-specific mechanics - each is a single stock effect
    /// (GainResource/PlaceSpy/Assassinate/ReturnUnit/Supplant/MoveUnit) with no Choose-one, no
    /// custom targeting, no multi-step chain. Only rows 1 (positive), 4 (wrong-player), and 7
    /// (double-dispatch/replay) apply - row 2 (choose-one) and row 3 (no-valid-target fallback
    /// via Alternative) don't exist for these cards, and row 9 (DTO round-trip) is already
    /// covered generically for every command type these cards can produce
    /// (CommandSerializationTests.cs/NewCommandDtoRoundTripTests.cs) - not card-specific, so
    /// not repeated here. Batched into one file rather than 8 near-empty ones.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class TrivialPrimitiveCardsScenarioTests
    {
        /// <summary>
        /// Deploys Red at a real node and marks an adjacent node as Blue-occupied - the shared
        /// setup every targeting card below needs (Assassinate/ReturnUnit/Supplant/MoveUnit all
        /// require Presence, granted here via the deployed Red troop's adjacency).
        /// </summary>
        private static (Player red, MapNode blueTarget) SetupRedWithAdjacentBlueTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var blueTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            blueTarget.Occupant = blue.Color;

            return (red, blueTarget);
        }

        // --- core_house_guard: "Gain 2 Power" - no targeting at all. ---

        [TestMethod]
        public void PlayHouseGuard_GrantsTwoPower()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "core_house_guard");

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Power);
        }

        [TestMethod]
        public void PlayHouseGuardCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "core_house_guard");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayHouseGuardCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "core_house_guard");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.Power, "Should have applied exactly once, not twice.");
        }

        // --- core_priestess: "Gain 2 Influence" - identical shape, different resource. ---

        [TestMethod]
        public void PlayPriestess_GrantsTwoInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "core_priestess");

            scenario.PlayCard(card);

            Assert.AreEqual(2, red.Influence);
        }

        [TestMethod]
        public void PlayPriestessCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "core_priestess");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayPriestessCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "core_priestess");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(2, red.Influence, "Should have applied exactly once, not twice.");
        }

        // --- drow_spy_master: "Place a Spy" - mandatory PlaceSpy, one site click. ---

        [TestMethod]
        public void PlaySpyMaster_PlacesASpyAtTheChosenSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "drow_spy_master");
            var site = scenario.Context.MapManager.Sites.First();

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(null, site);

            Assert.Contains(red.Color, site.Spies);
        }

        [TestMethod]
        public void PlaySpyMasterCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "drow_spy_master");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlaySpyMasterCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "drow_spy_master");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingPlaceSpy, scenario.Context.ActionSystem.CurrentState, "Should still be waiting for exactly the one site click the first play triggered.");
        }

        // --- test_assassin: "Assassinate a troop" - mandatory Assassinate. ---

        [TestMethod]
        public void PlayDrowAssassin_AssassinatesTheChosenTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_assassin");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(blueTarget, null);

            Assert.AreEqual(PlayerColor.None, blueTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void PlayDrowAssassinCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "test_assassin");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayDrowAssassinCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_assassin");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
        }

        // --- test_guard: "Return a troop" - mandatory ReturnUnit. ---

        [TestMethod]
        public void PlayCityGuard_ReturnsTheChosenTroop()
        {
            var scenario = MatchScenario.Build();
            var (_, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_guard");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturn, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(blueTarget, null);

            Assert.AreEqual(PlayerColor.None, blueTarget.Occupant);
        }

        [TestMethod]
        public void PlayCityGuardCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "test_guard");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayCityGuardCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_guard");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingReturn, scenario.Context.ActionSystem.CurrentState);
        }

        // --- test_infiltrator: "Supplant a troop" - mandatory Supplant (needs troops in barracks too). ---

        [TestMethod]
        public void PlayEliteInfiltrator_SupplantsTheChosenTroop()
        {
            var scenario = MatchScenario.Build();
            var (red, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_infiltrator");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(blueTarget, null);

            Assert.AreEqual(red.Color, blueTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void PlayEliteInfiltratorCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "test_infiltrator");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayEliteInfiltratorCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_infiltrator");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
        }

        // --- test_blade_dancer: "Assassinate a troop. Focus - Gain 3 Power." ---

        [TestMethod]
        public void PlayShadowBladeDancer_AssassinatesAndGrantsNoBonusWithoutFocus()
        {
            var scenario = MatchScenario.Build();
            var (red, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_blade_dancer"); // Only card in hand - no Focus.

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(blueTarget, null);

            Assert.AreEqual(PlayerColor.None, blueTarget.Occupant);
            Assert.AreEqual(0, red.Power, "RequiresFocus effect must not fire without Focus.");
        }

        [TestMethod]
        public void PlayShadowBladeDancerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "test_blade_dancer");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayShadowBladeDancerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_blade_dancer");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
        }

        // --- test_displacer: "Move an enemy troop." - mandatory MoveUnit (2-click: source, destination). ---

        [TestMethod]
        public void PlayDisplacerBeast_MovesTheEnemyTroopToAnEmptyNode()
        {
            var scenario = MatchScenario.Build();
            var (_, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_displacer");
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != blueTarget);

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(blueTarget, null); // Pick the enemy troop to move.
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(destination, null); // Pick where it goes.

            Assert.AreEqual(PlayerColor.None, blueTarget.Occupant);
            Assert.AreEqual(PlayerColor.Blue, destination.Occupant);
        }

        [TestMethod]
        public void PlayDisplacerBeastCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "test_displacer");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayDisplacerBeastCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "test_displacer");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);
        }
    }
}
