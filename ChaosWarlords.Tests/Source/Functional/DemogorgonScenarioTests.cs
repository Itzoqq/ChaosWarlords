using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Demogorgon ("Devour a card in your hand to Supplant 2
    /// white troops. Each opponent recruits 2 Insane Outcasts.") - the first card to combine
    /// TWO leftover primitives from this session: SupplantStrategy.SupportsRepeat (previously
    /// unwired - see planning.txt's own "still open" note, closed here the same way
    /// AssassinateStrategy already supports Deathblade's "Assassinate 2 troops") and
    /// CardEffect.AppliesToEachOpponent (Ghoul). The Devour-&gt;Supplant half is Wight's exact
    /// cost-arrow shape (IsOptional Devour, no Alternative - declining or having nothing to
    /// devour grants nothing for that half) with Amount=2/TargetNeutralTroopOnly=true bolted on;
    /// ForceRecruit is a fully independent top-level effect that fires regardless of what
    /// happened with the Devour/Supplant half. Loads the REAL "demogorgon" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DemogorgonScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with at least 2 empty neighbors, then marks 2 of those
        /// neighbors as NEUTRAL (white) troop spaces - Red has Presence at both via the deployed
        /// troop's adjacency. Mirrors DeathbladeScenarioTests' identical helper, but with
        /// Neutral occupants (TargetNeutralTroopOnly) instead of an enemy color.
        /// </summary>
        private static (Player red, MapNode target1, MapNode target2) SetupRedWithTwoAdjacentNeutralTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Count(neighbor => neighbor.Occupant == PlayerColor.None) >= 2);
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));

            var emptyNeighbors = redNode.Neighbors.Where(n => n.Occupant == PlayerColor.None).Take(2).ToList();
            emptyNeighbors[0].Occupant = PlayerColor.Neutral; // Setup only - not going through a command.
            emptyNeighbors[1].Occupant = PlayerColor.Neutral;

            return (red, emptyNeighbors[0], emptyNeighbors[1]);
        }

        // --- Row 1: positive/happy path ---

        [TestMethod]
        public void PlayDemogorgon_AcceptDevourAndSupplantBothTroops_AlsoForcesTheOpponentToRecruitTwoOutcasts()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            var fodder = scenario.GiveCard(PlayerColor.Red, "core_noble");
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            scenario.PlayCard(demogorgon);
            Assert.HasCount(1, scenario.Interactions, "A real Supplant target exists, so the optional-effect popup should fire.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingDevourHand, scenario.Context.ActionSystem.CurrentState);
            scenario.SelectDevourCard(fodder);

            Assert.IsFalse(red.Hand.Contains(fodder), "The fodder card should have been devoured.");
            Assert.AreEqual(CardLocation.Void, fodder.Location);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(target1, null);
            Assert.AreEqual(red.Color, target1.Occupant, "First Supplant should have placed Red's troop.");
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(red.Color, target2.Occupant, "Second Supplant should have placed Red's troop.");
            Assert.AreEqual(2, red.TrophyHall, "Both Supplant assassinate halves should award a trophy.");

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The whole chain should have settled.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile, "Each opponent recruits 2 Insane Outcasts - independent of the Devour/Supplant half.");
            Assert.AreEqual(2, blue.DiscardPile.Count(c => c.DefinitionId == "insane_outcast"));
        }

        [TestMethod]
        public void PlayDemogorgon_DeclineDevour_SkipsSupplantButStillForcesTheRecruit()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            scenario.GiveCard(PlayerColor.Red, "core_noble");
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            scenario.PlayCard(demogorgon);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(PlayerColor.Neutral, target1.Occupant, "Declining must leave both troops untouched.");
            Assert.AreEqual(PlayerColor.Neutral, target2.Occupant);
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile, "ForceRecruit is a fully independent top-level effect - it must still fire regardless of the decline.");
        }

        [TestMethod]
        public void PlayDemogorgon_EmptyHand_SkipsThePopupEntirely_ButStillForcesTheRecruit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon"); // Only card in hand.
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            scenario.PlayCard(demogorgon);

            Assert.IsEmpty(scenario.Interactions, "No valid Devour target (empty hand) means no popup - unlike Wight, there's no Alternative to fall into either.");
            Assert.AreEqual(0, red.TrophyHall);
            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile, "ForceRecruit must still fire.");
        }

        [TestMethod]
        public void PlayDemogorgon_OnlyOneNeutralTroopReachable_ResolvesSupplantEarlyThenStillForcesTheRecruit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var onlyTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            onlyTarget.Occupant = PlayerColor.Neutral; // The ONLY neutral troop anywhere on the board.

            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            var fodder = scenario.GiveCard(PlayerColor.Red, "core_noble");
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            scenario.PlayCard(demogorgon);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(fodder);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(onlyTarget, null);

            Assert.AreEqual(red.Color, onlyTarget.Occupant);
            Assert.AreEqual(1, red.TrophyHall, "Only 1 neutral troop existed - the effect must not demand an impossible 2nd.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "With no more valid targets, the effect must resolve instead of leaving the player stuck.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile);
        }

        [TestMethod]
        public void PlayDemogorgon_ThreePlayerMatch_ForcesEveryOpponentToRecruitTwoEach()
        {
            var scenario = MatchScenario.Build(playerColors: new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Orange });
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var orange = scenario.Player(PlayerColor.Orange);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon"); // Only card in hand - Devour half skips.
            int blueDiscardCountBefore = blue.DiscardPile.Count;
            int orangeDiscardCountBefore = orange.DiscardPile.Count;
            int redDiscardCountBefore = red.DiscardPile.Count;

            scenario.PlayCard(demogorgon);

            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile, "Every opponent, not just one, must recruit twice.");
            Assert.HasCount(orangeDiscardCountBefore + 2, orange.DiscardPile);
            Assert.HasCount(redDiscardCountBefore, red.DiscardPile, "The real card-playing player is never their own opponent.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayDemogorgonCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var demogorgon = scenario.GiveCard(PlayerColor.Blue, "demogorgon");

            scenario.AssertRejected(new PlayCardCommand(demogorgon));

            Assert.Contains(demogorgon, blue.Hand, "Demogorgon should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/already-moved target for the SECOND Supplant repeat specifically ---

        [TestMethod]
        public void SupplantCommand_TargetingTheAlreadySupplantedNode_IsRejectedForTheSecondDemogorgonTarget()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            var fodder = scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(demogorgon);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(fodder);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Still 1 more target owed.");

            // target1 is now Red's own troop (already supplanted) - re-targeting it for the
            // SECOND repeat must be rejected (own troop, not a neutral one).
            var forgedCommand = new SupplantCommand(target1.Id, demogorgon.Id);
            scenario.AssertRejected(forgedCommand, "An already-supplanted node must be rejected as a target for the 2nd repeat.");
            Assert.AreEqual(1, red.TrophyHall, "The rejected re-target must not grant a second trophy.");
        }

        // --- Row 7: double-dispatch/replay, for every command type this card can produce ---

        [TestMethod]
        public void PlayDemogorgonCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon"); // Only card in hand - Devour half skips.
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            scenario.DispatchTwice(new PlayCardCommand(demogorgon));

            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile, "The forced recruit must have happened exactly once (2 copies), not twice (4 copies).");
        }

        [TestMethod]
        public void SupplantCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            var fodder = scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(demogorgon);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(fodder);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);

            scenario.DispatchTwice(new SupplantCommand(target1.Id, demogorgon.Id));

            Assert.AreEqual(1, red.TrophyHall, "target1 should have been supplanted exactly once, not twice.");
            Assert.AreEqual(red.Color, target1.Occupant);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed - exactly 1 more target should still be owed.");
            Assert.AreEqual(PlayerColor.Neutral, target2.Occupant, "target2 must remain untouched by the rejected replay.");

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 9: DTO round-trip for every command type this card can produce ---

        [TestMethod]
        public void PlayCardCommand_DtoRoundTrip_StillPlaysDemogorgonAndForcesTheRecruit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon"); // Only card in hand - Devour half skips.
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            var command = new PlayCardCommand(demogorgon);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as PlayCardCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a PlayCardCommand.");

            scenario.Dispatch(hydrated);

            Assert.HasCount(blueDiscardCountBefore + 2, blue.DiscardPile);
        }

        [TestMethod]
        public void SupplantCommand_DtoRoundTrip_StillSupplantsThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentNeutralTroops(scenario);
            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            var fodder = scenario.GiveCard(PlayerColor.Red, "core_noble");

            scenario.PlayCard(demogorgon);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.SelectDevourCard(fodder);

            var command = new SupplantCommand(target1.Id, demogorgon.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as SupplantCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);
            Assert.AreEqual(command.CardId, hydrated.CardId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, target1.Occupant);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");
        }
    }
}
