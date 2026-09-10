using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Gibbering Mouther ("Deploy 2 troops, then choose an
    /// opponent with a troop adjacent to at least 1 of them. That opponent recruits an Insane
    /// Outcast.") - the first card to need an IMMEDIATE, node-tracked Deploy (EffectType.
    /// DeployTroop/ActionState.TargetingDeployTroop, see that state's own doc comment for why
    /// this is distinct from the existing GainResource(Troops)/PendingFreeTroops shape every
    /// other "Deploy N troops" card uses), an adjacency-filtered SelectOpponent eligibility mode
    /// (CardEffect.RequiresAdjacencyToRecentDeploys, see SelectOpponentEligibility), and a new
    /// EffectType.ForceRecruit (gives a specific card - here "insane_outcast" - directly to
    /// whoever SelectOpponent chose, bypassing the market row). Loads the REAL
    /// "gibbering_mouther" entry out of the REAL cards.json and dispatches every command through
    /// a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class GibberingMoutherScenarioTests
    {
        private static Site ObsidianFortress(MatchScenario scenario) =>
            scenario.Context.MapManager.Sites.First(s => s.Name == "Obsidian Fortress"); // 6 nodes, room for 3 mutually-adjacent empty nodes.

        [TestMethod]
        public void PlayGibberingMouther_DeployBothTroopsThenChooseEligibleOpponent_ForcesTheRecruit()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            var nodeC = fortress.NodesInternal[2]; // Site nodes are a fully-connected mesh - mutually adjacent to A and B.
            nodeC.Occupant = blue.Color;
            int troopsInBarracksBefore = red.TroopsInBarracks;
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);

            Assert.AreEqual(ActionState.TargetingDeployTroop, scenario.Context.ActionSystem.CurrentState, "Mandatory effect - no popup, straight into targeting.");
            scenario.ClickTarget(nodeA, null);

            Assert.AreEqual(red.Color, nodeA.Occupant);
            Assert.AreEqual(ActionState.TargetingDeployTroop, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");
            Assert.HasCount(1, scenario.Context.ActionSystem.PendingDeployedNodes);

            scenario.ClickTarget(nodeB, null);

            Assert.AreEqual(red.Color, nodeB.Occupant);
            Assert.AreEqual(troopsInBarracksBefore, red.TroopsInBarracks, "Card-granted Deploy is funded like GainResource(Troops) - PendingFreeTroops, not the barracks.");
            Assert.AreEqual(0, red.PendingFreeTroops, "Both credited free troops should already be fully consumed by the 2 immediate deploys.");
            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState, "Both deploys done - chains into choosing the eligible opponent.");

            scenario.Dispatch(new SelectOpponentCommand(blue.Color));

            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile, "Blue should have recruited exactly 1 card.");
            Assert.IsTrue(blue.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"), "The forced recruit must specifically be an Insane Outcast.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "ForceRecruit is automatic - the whole chain settles back to Normal in this one dispatch.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "The forced-actor override must be fully released once the chain completes.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayGibberingMouther_NoValidDeployTargetAnywhere_SkipsTheWholeEffectCleanly()
        {
            // Red has Presence (a spy) but every node it could reach is occupied - the
            // mandatory DeployTroop effect must resolve as a clean no-op (see
            // CardEffectApplier.ApplyDeployTroop), not stall waiting for an impossible click.
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var crystalCave = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            crystalCave.AddSpy(red.Color); // Presence via spy only - a spy does NOT propagate Presence to adjacent nodes outside its own site (only troops do), so this can't accidentally open a deploy target elsewhere.
            foreach (var node in crystalCave.NodesInternal)
            {
                node.Occupant = blue.Color;
            }

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(0, red.PendingFreeTroops, "Nothing should have been credited if nothing could ever be deployed.");
        }

        [TestMethod]
        public void PlayGibberingMouther_OnlyOneValidDeployTargetExists_ResolvesEarlyThenSkipsSelectOpponentIfNoneEligible()
        {
            // Mirrors Deathblade's "no more valid targets - resolve early instead of waiting
            // for an impossible 2nd repeat" fallback (ShouldRepeatCurrentEffect), exercised here
            // for the new DeployTroop effect type specifically. Also proves the eligibility
            // check that follows only ever sees the ONE node that was actually deployed to.
            // Every OTHER node on the board is pre-occupied (Presence propagates from a whole
            // SITE having a friendly troop anywhere in it - see MapRuleEngine.IsSourceOfPresence
            // - not just from the specific occupied node, so a single "isolated" empty node
            // elsewhere on the board can't be relied on to stay unreachable after the first
            // deploy; occupying literally everything else sidesteps that entirely).
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var lastEmptyNode = scenario.Context.MapManager.Nodes.First();
            foreach (var node in scenario.Context.MapManager.Nodes)
            {
                if (node != lastEmptyNode)
                {
                    node.Occupant = PlayerColor.Neutral;
                }
            }

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            Assert.AreEqual(ActionState.TargetingDeployTroop, scenario.Context.ActionSystem.CurrentState);

            scenario.ClickTarget(lastEmptyNode, null);

            Assert.AreEqual(red.Color, lastEmptyNode.Occupant);
            // No further valid deploy target exists anywhere (every other node is occupied) -
            // and nobody has a troop adjacent to lastEmptyNode that isn't Neutral, so
            // SelectOpponent has no eligible target either - the whole chain must resolve
            // cleanly instead of stalling on either step.
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Only 1 of the 2 owed repeats had a legal target - must resolve early, then find no eligible opponent and end cleanly.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayGibberingMouther_DeploysSucceedButNoOpponentIsAdjacent_SkipsSelectOpponentCleanly()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            // Deliberately no Blue troop anywhere - nobody can possibly be eligible.

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.ClickTarget(nodeA, null);
            scenario.ClickTarget(nodeB, null);

            Assert.AreEqual(red.Color, nodeA.Occupant);
            Assert.AreEqual(red.Color, nodeB.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No eligible opponent anywhere - SelectOpponent must resolve cleanly, not stall.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
        }

        [TestMethod]
        public void SelectOpponentCommand_ForAnIneligibleOpponent_IsRejected_EvenWhileWindowIsOpen()
        {
            // 3-seat match: Blue is eligible (adjacent troop), Orange is not - proves rejection
            // is about THIS specific target's adjacency, not "nobody is eligible" (a different,
            // already-covered fallback).
            var scenario = MatchScenario.Build(playerColors: new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Orange });
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var orange = scenario.Player(PlayerColor.Orange);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            var nodeC = fortress.NodesInternal[2];
            nodeC.Occupant = blue.Color; // Adjacent to both deployed nodes - eligible.
            // Orange has no troop anywhere near the deployed nodes - not eligible.
            var orangeCard = scenario.GiveCard(PlayerColor.Orange, "core_house_guard");

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.ClickTarget(nodeA, null);
            scenario.ClickTarget(nodeB, null);
            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue's eligibility alone should have opened the window.");

            scenario.AssertRejected(new SelectOpponentCommand(orange.Color), "Orange has no troop adjacent to either deployed node.");

            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState, "Still waiting for a valid choice.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer);
            Assert.Contains(orangeCard, orange.Hand, "Nothing should have happened to Orange.");
        }

        [TestMethod]
        public void SelectOpponentCommand_TargetingSelf_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            var nodeC = fortress.NodesInternal[2];
            nodeC.Occupant = blue.Color;

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.ClickTarget(nodeA, null);
            scenario.ClickTarget(nodeB, null);

            scenario.AssertRejected(new SelectOpponentCommand(red.Color), "The active player cannot choose themself.");

            Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayGibberingMoutherCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var gibberingMouther = scenario.GiveCard(PlayerColor.Blue, "gibbering_mouther");

            scenario.AssertRejected(new PlayCardCommand(gibberingMouther));

            Assert.Contains(gibberingMouther, blue.Hand, "Gibbering Mouther should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/nonexistent target for each targeting sub-step ---

        [TestMethod]
        public void DeployTroopCommand_ForAnAlreadyOccupiedNode_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var fortress = ObsidianFortress(scenario);
            var occupiedNode = fortress.NodesInternal[0];
            occupiedNode.Occupant = blue.Color;

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);

            var forgedCommand = new DeployTroopCommand(occupiedNode.Id, gibberingMouther.Id);
            scenario.AssertRejected(forgedCommand, "An occupied node is never a valid Deploy target.");
            Assert.AreEqual(blue.Color, occupiedNode.Occupant);
        }

        [TestMethod]
        public void SelectOpponentCommand_ForAColorNotInTheMatch_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            var nodeC = fortress.NodesInternal[2];
            nodeC.Occupant = blue.Color;

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.ClickTarget(nodeA, null);
            scenario.ClickTarget(nodeB, null);

            scenario.AssertRejected(new SelectOpponentCommand(PlayerColor.Orange));
        }

        // --- Row 7: double-dispatch/replay, for every command type this card can produce ---

        [TestMethod]
        public void PlayGibberingMoutherCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            scenario.AsActivePlayer(PlayerColor.Red);
            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");

            scenario.DispatchTwice(new PlayCardCommand(gibberingMouther));

            Assert.AreEqual(ActionState.TargetingDeployTroop, scenario.Context.ActionSystem.CurrentState, "The card's effect should have started exactly once.");
        }

        [TestMethod]
        public void DeployTroopCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.DispatchTwice(new DeployTroopCommand(nodeA.Id, gibberingMouther.Id));

            Assert.AreEqual(red.Color, nodeA.Occupant);
            Assert.AreEqual(ActionState.TargetingDeployTroop, scenario.Context.ActionSystem.CurrentState, "The replay must not have advanced past the pending second deploy.");
        }

        [TestMethod]
        public void SelectOpponentCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            var nodeC = fortress.NodesInternal[2];
            nodeC.Occupant = blue.Color;
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.ClickTarget(nodeA, null);
            scenario.ClickTarget(nodeB, null);
            scenario.DispatchTwice(new SelectOpponentCommand(blue.Color));

            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile, "The forced recruit must have happened exactly once.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 9: DTO round-trip for every command type this card can produce ---

        [TestMethod]
        public void DeployTroopCommand_DtoRoundTrip_StillDeploysThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);

            var command = new DeployTroopCommand(nodeA.Id, gibberingMouther.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as DeployTroopCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.NodeId, hydrated!.NodeId);
            Assert.AreEqual(command.CardId, hydrated.CardId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(red.Color, nodeA.Occupant);
            Assert.AreEqual(ActionState.TargetingDeployTroop, scenario.Context.ActionSystem.CurrentState, "1 more repeat still owed.");
        }

        [TestMethod]
        public void SelectOpponentCommand_DtoRoundTrip_StillForcesTheRecruitThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing); // Post-setup: normal 2-troop Deploy needs the Play-phase Presence rules, not Setup's "exactly 1 ever" restriction.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var fortress = ObsidianFortress(scenario);
            var nodeA = fortress.NodesInternal[0];
            var nodeB = fortress.NodesInternal[1];
            var nodeC = fortress.NodesInternal[2];
            nodeC.Occupant = blue.Color;
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            var gibberingMouther = scenario.GiveCard(PlayerColor.Red, "gibbering_mouther");
            scenario.PlayCard(gibberingMouther);
            scenario.ClickTarget(nodeA, null);
            scenario.ClickTarget(nodeB, null);

            var command = new SelectOpponentCommand(blue.Color);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as SelectOpponentCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetPlayerColor, hydrated!.TargetPlayerColor);

            scenario.Dispatch(hydrated);

            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile);
        }
    }
}
