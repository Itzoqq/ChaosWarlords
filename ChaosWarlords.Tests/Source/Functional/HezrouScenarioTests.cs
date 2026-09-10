using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Hezrou ("Move an enemy troop. Promote the top card of
    /// your deck.") - the first shipped card using the new EffectType.PromoteTopOfDeck primitive
    /// (planning.txt TIER 1 item 6: "promote from more source locations", the deck-top sub-case).
    /// Unlike EffectType.PromoteFromPile (Hand/DiscardPile/Self, player-chosen), there is no
    /// targeting or player choice at all here - whatever sits on top of the deck is promoted,
    /// resolved via Player.TryPromoteTopOfDeck/PlayerStateManager.TryPromoteTopOfDeck. Loads the
    /// REAL "hezrou" entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher, mirroring TrivialPrimitiveCardsScenarioTests.cs's MoveUnit setup and
    /// CouncilMemberScenarioTests.cs's stale-target idiom (both also drive MoveUnit).
    ///
    /// Row 6 (unmet resource precondition) doesn't apply - neither MoveTroopCommand nor
    /// PromoteTopOfDeck has a resource cost of its own to fail, matching
    /// CouncilMemberScenarioTests.cs's own documented exclusion for the same reason.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class HezrouScenarioTests
    {
        /// <summary>
        /// Deploys Red at a real node and marks an adjacent node as Blue-occupied - matches
        /// TrivialPrimitiveCardsScenarioTests.cs's identical helper (MoveUnit needs Presence at
        /// the enemy troop's node).
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

        // --- Row 1: happy path - both effects apply. ---

        [TestMethod]
        public void PlayHezrou_MovesTheEnemyTroop_ThenPromotesTheTopOfDeck()
        {
            var scenario = MatchScenario.Build();
            var (red, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "hezrou");
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != blueTarget);
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(blueTarget, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(destination, null);

            Assert.AreEqual(PlayerColor.None, blueTarget.Occupant, "The enemy troop should have moved away.");
            Assert.AreEqual(PlayerColor.Blue, destination.Occupant);
            Assert.Contains(topOfDeck, red.InnerCircle, "The card that was on top of the deck should now be promoted.");
            Assert.DoesNotContain(topOfDeck, red.DeckManager.DrawPile);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 3: no-valid-target fallback for the MoveUnit half - the Promote half must
        // still apply even when there's nothing to move. ---

        [TestMethod]
        public void PlayHezrou_WithNoEnemyTroopToMove_StillPromotesTheTopOfDeck()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "hezrou"); // No troops anywhere on the board.
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "MoveUnit should have quietly no-opped - no targeting UI should ever have opened.");
            Assert.Contains(topOfDeck, red.InnerCircle, "The Promote half must still apply independently of the Move half.");
            Assert.DoesNotContain(topOfDeck, red.DeckManager.DrawPile);
        }

        // --- Row 5: stale/nonexistent target for the MoveUnit destination sub-step. ---

        [TestMethod]
        public void MoveTroopCommand_WithAStaleDestinationNodeId_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "hezrou");
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.PlayCard(card);
            scenario.ClickTarget(blueTarget, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new MoveTroopCommand(blueTarget.Id, 999999, card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent destination node id must be rejected.");

            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState, "The rejected command must not have advanced the sequence.");
            Assert.DoesNotContain(topOfDeck, red.InnerCircle, "The Promote half must not have fired off the back of a rejected command.");
        }

        // --- Row 4: wrong-player dispatch. ---

        [TestMethod]
        public void PlayHezrouCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "hezrou");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        // --- Row 7: double-dispatch/replay. ---

        [TestMethod]
        public void PlayHezrouCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "hezrou"); // No enemy troop - resolves instantly.
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.Contains(topOfDeck, red.InnerCircle);
            Assert.HasCount(1, red.InnerCircle, "The Promote half must have applied exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void MoveTroopCommand_DtoRoundTrip_StillMovesTheTroopAndPromotesTheTopOfDeck()
        {
            var scenario = MatchScenario.Build();
            var (red, blueTarget) = SetupRedWithAdjacentBlueTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "hezrou");
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != blueTarget);
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.PlayCard(card);
            scenario.ClickTarget(blueTarget, null); // Source click - no command dispatched yet.
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);

            var command = new MoveTroopCommand(blueTarget.Id, destination.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, scenario.Context) as MoveTroopCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a MoveTroopCommand.");

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, blueTarget.Occupant);
            Assert.AreEqual(PlayerColor.Blue, destination.Occupant);
            Assert.Contains(topOfDeck, red.InnerCircle, "The Promote half should still fire off the back of the hydrated command.");
        }
    }
}
