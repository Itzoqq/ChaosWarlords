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
    /// Standing test-matrix coverage for Revenant ("Assassinate two troops. Then, if you have 8
    /// or more troops in your trophy hall, promote this card.") - the first shipped card using
    /// EffectType.PromoteSelf (an unconditional, deterministic self-promotion, deferred to end
    /// of turn via MatchContext.CardsMarkedForTurnEndPromote - distinct from EffectType.Promote's
    /// floating "credit redeemable against OTHER cards" system) and ConditionType.
    /// TrophyHallCount. Reuses Deathblade's unrestricted "Assassinate 2 troops" shape (no
    /// TargetNeutralTroopOnly - Revenant assassinates ANY troop). Loads the REAL "revenant"
    /// entry out of the REAL cards.json and dispatches every command through a REAL
    /// CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class RevenantScenarioTests
    {
        /// <summary>
        /// Deploys Red at a node with at least 2 empty neighbors, then marks 2 of those
        /// neighbors as Blue-occupied troop spaces - same helper shape as
        /// DeathbladeScenarioTests.cs (Revenant's Assassinate is equally unrestricted).
        /// </summary>
        private static (Player red, MapNode target1, MapNode target2) SetupRedWithTwoAdjacentEnemyTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n =>
                scenario.Context.MapManager.CanDeployAt(n, red.Color) &&
                n.Neighbors.Count(neighbor => neighbor.Occupant == PlayerColor.None) >= 2);
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));

            var emptyNeighbors = redNode.Neighbors.Where(n => n.Occupant == PlayerColor.None).Take(2).ToList();
            emptyNeighbors[0].Occupant = PlayerColor.Blue; // Setup only - not going through a command.
            emptyNeighbors[1].Occupant = PlayerColor.Blue;

            return (red, emptyNeighbors[0], emptyNeighbors[1]);
        }

        // --- Row 1: positive/happy path, condition MET ---

        [TestMethod]
        public void PlayRevenant_TrophyHallAlreadyAtSix_AssassinatesBothAndSelfPromotesAtEndOfTurn()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            red.TrophyHall = 6; // +2 from this card's own assassinations reaches the 8 threshold.
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            scenario.ClickTarget(target2, null);

            Assert.AreEqual(8, red.TrophyHall, "Both assassinations should have landed.");
            Assert.AreEqual(CardLocation.Played, card.Location, "Self-promotion stays 'Played' until end of turn (deferred, same as Devour(Self)'s pattern).");
            Assert.Contains(card, scenario.Context.CardsMarkedForTurnEndPromote);
            Assert.DoesNotContain(card, red.InnerCircle.ToList(), "Not actually promoted yet - only marked.");

            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(CardLocation.InnerCircle, card.Location);
            Assert.Contains(card, red.InnerCircle.ToList());
            Assert.DoesNotContain(card, red.DiscardPile.ToList(), "Promoted cards must not also end up in the discard pile.");
            Assert.IsEmpty(scenario.Context.CardsMarkedForTurnEndPromote, "The marker list must be cleared after processing.");
        }

        // --- Row 1b: positive/happy path, condition NOT met ---

        [TestMethod]
        public void PlayRevenant_TrophyHallBelowThreshold_AssassinatesBothButDiscardsNormallyAtEndOfTurn()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            red.TrophyHall = 5; // +2 reaches only 7 - below the 8 threshold.
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            scenario.ClickTarget(target2, null);

            Assert.AreEqual(7, red.TrophyHall);
            Assert.DoesNotContain(card, scenario.Context.CardsMarkedForTurnEndPromote, "Below the threshold - must not be marked for self-promotion.");

            scenario.Dispatch(new EndTurnCommand());

            Assert.AreEqual(CardLocation.DiscardPile, card.Location, "Without the condition, the card discards normally like any other played card.");
            Assert.DoesNotContain(card, red.InnerCircle.ToList());
        }

        // --- Row 3: no-valid-target fallback, plus the "fewer than requested targets exist" edge case ---

        [TestMethod]
        public void PlayRevenant_ExactlyOneEnemyTroopReachable_AssassinatesItAndNeverChecksThePromoteCondition()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.TrophyHall = 20; // Would easily meet the threshold, if the chain ever got there.
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var onlyTarget = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            onlyTarget.Occupant = PlayerColor.Blue; // The ONLY enemy troop anywhere on the board.

            var card = scenario.GiveCard(PlayerColor.Red, "revenant");
            scenario.PlayCard(card);
            scenario.ClickTarget(onlyTarget, null);

            Assert.AreEqual(PlayerColor.None, onlyTarget.Occupant);
            Assert.AreEqual(21, red.TrophyHall, "Only 1 troop existed - the effect must not demand an impossible 2nd.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "With no more valid targets, the effect must resolve (and chain into PromoteSelf) instead of leaving the player stuck.");
            Assert.Contains(card, scenario.Context.CardsMarkedForTurnEndPromote, "The chain should still have reached PromoteSelf even though only 1 of the 2 requested Assassinate targets existed.");
        }

        [TestMethod]
        public void PlayRevenant_NoTroopsAnywhereOnTheBoard_SkipsAssassinateAndNeverMarksForPromotion()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.TrophyHall = 20;
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.DoesNotContain(card, scenario.Context.CardsMarkedForTurnEndPromote, "No Assassinate ever happened, so its OnSuccess chain (PromoteSelf) must never have run either.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayRevenantCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "revenant");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Revenant should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 5: stale/already-moved target for the SECOND repeat specifically ---

        [TestMethod]
        public void AssassinateCommand_TargetingTheAlreadyAssassinatedNode_IsRejectedForTheSecondRevenantTarget()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");

            scenario.PlayCard(card);
            scenario.ClickTarget(target1, null);
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Still 1 more target owed.");

            var forgedCommand = new AssassinateCommand(target1.Id, card.Id);
            scenario.AssertRejected(forgedCommand, "An already-assassinated (now empty) node must be rejected as a target for the 2nd repeat.");
            Assert.AreEqual(1, red.TrophyHall);
        }

        [TestMethod]
        public void AssassinateCommand_TargetingANonexistentNode_IsRejectedWhileRevenantEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);

            var forgedCommand = new AssassinateCommand(targetNodeId: 999999, cardId: card.Id);
            scenario.AssertRejected(forgedCommand, "A stale/nonexistent node id must be rejected.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void AssassinateCommand_DispatchedTwiceAgainstTheSameFirstTarget_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, target2) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");

            scenario.PlayCard(card);
            scenario.DispatchTwice(new AssassinateCommand(target1.Id, card.Id));

            Assert.AreEqual(1, red.TrophyHall, "target1 should have been assassinated exactly once, not twice.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "The repeat must not have been double-consumed - exactly 1 more target should still be owed.");
            Assert.AreEqual(PlayerColor.Blue, target2.Occupant);

            scenario.ClickTarget(target2, null);
            Assert.AreEqual(2, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void AssassinateCommand_DtoRoundTrip_StillAssassinatesTheFirstTargetThroughARealDispatch()
        {
            var scenario = MatchScenario.Build();
            var (red, target1, _) = SetupRedWithTwoAdjacentEnemyTroops(scenario);
            red.TrophyHall = 20;
            var card = scenario.GiveCard(PlayerColor.Red, "revenant");
            scenario.PlayCard(card);

            var command = new AssassinateCommand(target1.Id, card.Id);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as AssassinateCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into an AssassinateCommand.");
            Assert.AreEqual(command.TargetNodeId, hydrated!.TargetNodeId);

            scenario.Dispatch(hydrated);

            Assert.AreEqual(PlayerColor.None, target1.Occupant);
            Assert.AreEqual(21, red.TrophyHall);
        }
    }
}
