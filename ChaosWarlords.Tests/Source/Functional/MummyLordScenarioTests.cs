using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Mummy Lord ("Choose 2 times: Assassinate a white troop.
    /// Or, take a white troop from any trophy hall and deploy it anywhere on the board.") - the
    /// first shipped card using EffectType.DeployFromTrophyHall (TROPHY-HALL-AS-TROOP-RESERVOIR,
    /// planning.txt). Unlike Weaponmaster (WeaponmasterScenarioTests.cs), where the "accept"
    /// branch (Deploy) is always trivially available, BOTH of Mummy Lord's branches are real
    /// targeting effects with their own HasValidTargets gate - see the row-3 tests below for how
    /// that changes the "no valid target" shape. Loads the REAL "mummy_lord" entry out of the
    /// REAL cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MummyLordScenarioTests
    {
        private static (Player red, MapNode t1, MapNode t2) SetupRedWithTwoReachableNeutralTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 2);
            site.AddSpy(red.Color);
            var nodes = site.NodesInternal.Take(2).ToList();
            nodes[0].Occupant = PlayerColor.Neutral;
            nodes[1].Occupant = PlayerColor.Neutral;
            return (red, nodes[0], nodes[1]);
        }

        private static (MapNode d1, MapNode d2) FindTwoEmptyNodes(MatchScenario scenario, params MapNode[] excluding)
        {
            var empty = scenario.Context.MapManager.Nodes
                .Where(n => n.Occupant == PlayerColor.None && !excluding.Contains(n))
                .Take(2)
                .ToList();
            return (empty[0], empty[1]);
        }

        // --- Row 1: happy path, accept branch (Assassinate) ---

        [TestMethod]
        public void PlayMummyLord_AcceptingBothRounds_AssassinatesTwoReachableNeutralTroops()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(t1, null);

            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(t2, null);

            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(2, red.TrophyHallByColor[PlayerColor.Neutral]);
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(PlayerColor.None, t2.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 1b: happy path, decline branch (DeployFromTrophyHall) ---

        [TestMethod]
        public void PlayMummyLord_DecliningBothRounds_TakesWhiteTroopsFromTrophyHallAndDeploysThem()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(2, PlayerColor.Neutral);
            var (d1, d2) = FindTwoEmptyNodes(scenario, t1, t2);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingDeployFromTrophyHall, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(d1, null);

            Assert.AreEqual(red.Color, d1.Occupant, "The reclaimed troop must be deployed as RED's own troop.");
            Assert.AreEqual(1, blue.TrophyHall, "Blue's trophy hall must have lost exactly one Neutral troop.");

            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(d2, null);

            Assert.AreEqual(red.Color, d2.Occupant);
            Assert.AreEqual(0, blue.TrophyHall);
            Assert.AreEqual(PlayerColor.Neutral, t1.Occupant, "Neither reachable troop should have been touched.");
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 2: choose-one mutual exclusivity, mixed across the 2 rounds ---

        [TestMethod]
        public void PlayMummyLord_MixedChoices_EachRoundResolvesIndependently()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            var (d1, _) = FindTwoEmptyNodes(scenario, t1, t2);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false); // Round 1: DeployFromTrophyHall (from Blue).
            scenario.ClickTarget(d1, null);
            // Round 1's DeployFromTrophyHall doesn't touch the board's Neutral troops, so
            // round 2's Assassinate is unaffected - unlike the reverse order, where accepting
            // Assassinate FIRST would add a Neutral trophy to RED's OWN hall, making a later
            // DeployFromTrophyHall round ambiguous between Red and Blue.
            scenario.RespondToLatestInteraction(accept: true); // Round 2: Assassinate.
            scenario.ClickTarget(t1, null);

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant, "Round 2 only assassinated t1 - t2 was never targeted.");
            Assert.AreEqual(red.Color, d1.Occupant);
            Assert.AreEqual(0, blue.TrophyHall);
        }

        // --- Row 3a: no-valid-target fallback - neither branch has any valid target at all ---

        [TestMethod]
        public void PlayMummyLord_NeitherBranchHasAnyValidTarget_DoesNothingAtAllCleanly()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            // No Neutral troops anywhere to assassinate, and no trophy hall has a Neutral troop.
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "Neither branch has a valid target - no popup should ever be raised.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 3b: Assassinate unavailable but DeployFromTrophyHall IS - skips straight to
        // its targeting with NO popup, since there's no real "choice" to present ---

        [TestMethod]
        public void PlayMummyLord_AssassinateUnavailableButTrophyHallAvailable_SkipsStraightToDeployTargetingWithNoPopup()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            // No Neutral troops on the board at all to assassinate.
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "Assassinate's own gate fails outright (no valid target anywhere), so round 1 falls straight to its Alternative with no confirmation popup - matching Weaponmaster's identical Assassinate-as-Alternative precedent.");
            Assert.AreEqual(ActionState.TargetingDeployFromTrophyHall, scenario.Context.ActionSystem.CurrentState);

            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            scenario.ClickTarget(destination, null);

            Assert.AreEqual(red.Color, destination.Occupant);
            Assert.AreEqual(0, blue.TrophyHall);
        }

        // --- Row 3c: the decline branch's OWN target turns out unavailable - round 2 must
        // still be offered (mirrors Weaponmaster's DeclineWithNoNeutralTroopsAnywhere test) ---

        [TestMethod]
        public void PlayMummyLord_DeclineIntoATrophyHallWithNoValidTarget_StillOffersRoundTwo()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            // Deliberately no eligible trophy hall - declining round 1 finds nothing to take.
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false); // Nothing to take from any trophy hall.

            Assert.HasCount(2, scenario.Interactions, "Round 2 must still be offered despite round 1's decline finding no target.");
            Assert.AreNotEqual(ActionState.TargetingDeployFromTrophyHall, scenario.Context.ActionSystem.CurrentState, "Round 1's DeployFromTrophyHall must have been skipped, not left waiting for an impossible click.");
            scenario.RespondToLatestInteraction(accept: true); // Round 2: Assassinate.
            scenario.ClickTarget(t1, null);

            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant, "t2 was never targeted - only 1 round's worth of Assassinate happened.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayMummyLordCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "mummy_lord");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Mummy Lord should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions);
        }

        // --- Row 5: stale/nonexistent/already-resolved target, for BOTH command types ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForALaterRound()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(t1, null);
            Assert.AreEqual(1, red.TrophyHall);
            scenario.RespondToLatestInteraction(accept: true);

            var forgedCommand = new AssassinateCommand(t1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "t1 is already empty - must be rejected as a target for round 2.");
            Assert.AreEqual(1, red.TrophyHall);

            scenario.ClickTarget(t2, null);
            Assert.AreEqual(2, red.TrophyHall);
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileMummyLordEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void DeployFromTrophyHallCommand_TargetingANonexistentNode_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var forgedCommand = new DeployFromTrophyHallCommand(999999, PlayerColor.Blue, PlayerColor.Neutral, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
            Assert.AreEqual(1, blue.TrophyHall, "Nothing should have been removed from Blue's trophy hall.");
            _ = red;
        }

        [TestMethod]
        public void DeployFromTrophyHallCommand_TargetingAnOccupiedNode_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            var forgedCommand = new DeployFromTrophyHallCommand(t1.Id, PlayerColor.Blue, PlayerColor.Neutral, card.Id);
            scenario.AssertRejected(forgedCommand, "t1 is occupied by a Neutral troop - not a legal deploy destination.");
            Assert.AreEqual(PlayerColor.Neutral, t1.Occupant);
            _ = red; _ = t2;
        }

        [TestMethod]
        public void DeployFromTrophyHallCommand_NamingADifferentSourcePlayerThanResolved_IsRejected()
        {
            // Even though Black is ALSO eligible, ActionSystem/TrophyHallRuleEngine only ever
            // auto-resolves a SINGLE source at a time - since 2 players being simultaneously
            // eligible means HasValidTargets itself would have reported false (see
            // TrophyHallRuleEngine's own doc comment), this scenario is only reachable via a
            // forged command bypassing the input layer entirely, same shape as
            // GrazztScenarioTests' SupplantCommand_TargetingTheWrongSite test.
            var scenario = MatchScenario.Build(playerColors: new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black });
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingDeployFromTrophyHall, scenario.Context.ActionSystem.CurrentState);

            var black = scenario.Player(PlayerColor.Black);
            black.SetTrophyHall(1, PlayerColor.Neutral); // Made eligible AFTER targeting already resolved to Blue.
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);

            var forgedCommand = new DeployFromTrophyHallCommand(destination.Id, PlayerColor.Black, PlayerColor.Neutral, card.Id);
            scenario.AssertRejected(forgedCommand, "Black was not the source ActionSystem resolved when targeting opened (Blue was).");
            Assert.AreEqual(1, black.TrophyHall);
            Assert.AreEqual(1, blue.TrophyHall);
            _ = red; _ = t1; _ = t2;
        }

        // --- Row 7: double-dispatch/replay, for both command types ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.DispatchTwice(new AssassinateCommand(t1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "t1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant, "t2 must remain untouched by the rejected replay.");
        }

        [TestMethod]
        public void DeployFromTrophyHallCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.PlayCard(card);
            // No Neutral troop anywhere to assassinate - Assassinate's own gate fails outright,
            // so round 1 falls straight to DeployFromTrophyHall with no confirmation popup.
            Assert.AreEqual(ActionState.TargetingDeployFromTrophyHall, scenario.Context.ActionSystem.CurrentState);
            scenario.DispatchTwice(new DeployFromTrophyHallCommand(destination.Id, PlayerColor.Blue, PlayerColor.Neutral, card.Id));

            Assert.AreEqual(red.Color, destination.Occupant);
            Assert.AreEqual(0, blue.TrophyHall, "The trophy-hall removal must not have been applied twice (it was already 1, floored at 0).");
        }

        // --- Row 9: DTO round-trip for both command types this card can produce ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheFirstTargetThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, _) = SetupRedWithTwoReachableNeutralTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);

            var command = new AssassinateCommand(t1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void DeployFromTrophyHallCommand_DtoRoundTrip_StillDeploysThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            blue.SetTrophyHall(1, PlayerColor.Neutral);
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");
            scenario.PlayCard(card);
            // No Neutral troop anywhere to assassinate - Assassinate's own gate fails outright,
            // so round 1 falls straight to DeployFromTrophyHall with no confirmation popup.
            Assert.AreEqual(ActionState.TargetingDeployFromTrophyHall, scenario.Context.ActionSystem.CurrentState);

            var command = new DeployFromTrophyHallCommand(destination.Id, PlayerColor.Blue, PlayerColor.Neutral, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as DeployFromTrophyHallCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);
            Assert.AreEqual(command.SourcePlayerColor, hydrated.SourcePlayerColor);
            Assert.AreEqual(command.TroopColor, hydrated.TroopColor);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, destination.Occupant);
            Assert.AreEqual(0, blue.TrophyHall);
        }

        // --- Double-dispatch on the card play itself ---

        [TestMethod]
        public void PlayMummyLordCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "mummy_lord");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(0, scenario.Interactions, "Neither branch has a valid target in this bare setup - no popup at all, and it must not have fired twice either.");
        }
    }
}
