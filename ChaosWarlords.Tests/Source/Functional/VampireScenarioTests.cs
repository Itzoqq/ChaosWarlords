using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Vampire ("Choose one: Supplant a troop. Or, promote a
    /// card from your discard pile, then gain 1 VP for every 3 cards in your inner circle.") -
    /// a choose-one (IsOptional Supplant primary / PromoteFromPile(DiscardPile) Alternative, the
    /// same shape as Cloaker's PlaceSpy/ReturnOwnSpy pair) whose Alternative branch chains into
    /// the new DynamicAmountSource.InnerCircleCount case: the VP amount is computed AFTER the
    /// chosen card has actually been promoted (CardEffectProcessor.PushSuccessorEffect fires
    /// GainResource's OnSuccess node only once PromoteCommand.Execute resolves the targeting
    /// effect), so it reflects the just-grown inner circle, not the pre-promotion count. Loads
    /// the REAL "vampire" entry out of the REAL cards.json and dispatches every command through a
    /// REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class VampireScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithAdjacentTroop(MatchScenario scenario, PlayerColor occupant)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var targetNode = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            targetNode.Occupant = occupant; // Setup only - not going through a command.

            return (red, targetNode);
        }

        // --- Row 1/2: positive/happy path + choose-one mutual exclusivity, accept branch ---

        [TestMethod]
        public void PlayVampire_AcceptSupplant_SupplantsAndGrantsNoVP()
        {
            var scenario = MatchScenario.Build();
            var (red, target) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Supplant vs. the Alternative.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(red.Color, target.Occupant, "Red's troop should have Supplanted the Blue one.");
            Assert.AreEqual(1, red.TrophyHall);
            Assert.AreEqual(0, red.VictoryPoints, "Choose-one mutual exclusivity: accepting Supplant must NOT also grant the Promote/VP Alternative.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 1: positive/happy path, decline branch - the headline dynamic-amount behavior ---

        [TestMethod]
        public void PlayVampire_DeclineAndPromoteFromDiscard_GrantsVPCountingTheFreshlyPromotedCard()
        {
            var scenario = MatchScenario.Build();
            // A real Supplant target must exist - otherwise the primary effect has no valid
            // target and the Alternative fires automatically with no popup at all (see
            // PlayVampire_NoTroopsAnywhereOnTheBoard_SkipsThePopupAndGoesStraightToPromote below),
            // which would not exercise an actual DECLINE of a real choice.
            var (red, _) = SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue);
            for (int i = 0; i < 5; i++) // 5 pre-existing inner circle cards.
            {
                red.AddToInnerCircle(scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!);
            }
            var discardCard = scenario.CardDatabase.GetCardById("core_priestess", scenario.Context.Random)!;
            red.DeckManager.AddToDiscard(discardCard);
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Setup check: a real Supplant target exists, so the choose-one popup must have been raised.");
            scenario.RespondToLatestInteraction(accept: false); // Decline: use the Alternative.

            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState);
            scenario.SelectPromoteFromPileCard(discardCard);

            Assert.Contains(discardCard, red.InnerCircle.ToList(), "The chosen card should have been promoted.");
            Assert.HasCount(6, red.InnerCircle, "5 pre-existing + 1 freshly promoted.");
            Assert.AreEqual(2, red.VictoryPoints, "6 inner circle cards / 3 per VP = 2 VP, counting the just-promoted card.");
            Assert.AreEqual(0, red.TrophyHall, "The Supplant half must not also have fired.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        // --- Row 3: no-valid-target fallback for the Supplant half - no popup, straight to the
        // Alternative's own targeting. ---

        [TestMethod]
        public void PlayVampire_NoTroopsAnywhereOnTheBoard_SkipsThePopupAndGoesStraightToPromote()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var discardCard = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            red.DeckManager.AddToDiscard(discardCard);
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "No valid Supplant target means no popup - the Alternative's own targeting opens directly.");
            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState);

            scenario.SelectPromoteFromPileCard(discardCard);

            Assert.Contains(discardCard, red.InnerCircle.ToList());
            Assert.AreEqual(0, red.VictoryPoints, "1 inner circle card / 3 must floor to 0 VP.");
        }

        [TestMethod]
        public void PlayVampire_NoTroopsAndEmptyDiscard_ResolvesWithNoEffectAtAll()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            Assert.IsEmpty(red.DiscardPile, "Setup check: fresh player, no discard yet.");
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            scenario.PlayCard(card);

            Assert.IsEmpty(scenario.Interactions, "Neither branch has a valid target - no popup should be raised.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Both branches unavailable should resolve cleanly, not hang.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(0, red.VictoryPoints);
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayVampireCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "vampire");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand, "Vampire should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions, "No popup should have been raised for a rejected command.");
        }

        // --- Row 5: stale/nonexistent target for the PromoteFromPile branch ---

        [TestMethod]
        public void PromoteCommand_ForVampireTargetingNonexistentCard_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.DeckManager.AddToDiscard(scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!);
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            // No troops anywhere - the Supplant half has no valid target, so the Alternative's
            // own targeting opens directly, with no choose-one popup to respond to.
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new PromoteCommand("this_card_id_does_not_exist", isChainedEffect: true));

            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real choice.");
            Assert.AreEqual(0, red.VictoryPoints);
        }

        // --- Row 7/8: double-dispatch/replay and rapid dispatch ---

        [TestMethod]
        public void PlayVampireCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithAdjacentTroop(scenario, PlayerColor.Blue); // A real Supplant target, so a popup is actually raised.
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        [TestMethod]
        public void PromoteCommand_ForVampire_DispatchedTwice_SecondDispatchIsRejectedAndDoesNotDoubleGrantVP()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var discardCard = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            red.DeckManager.AddToDiscard(discardCard);
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            // No troops anywhere - straight to TargetingPromoteFromPile, no popup involved.
            scenario.PlayCard(card);
            var command = scenario.Context.ActionSystem.HandlePromoteFromPileSelection(discardCard);
            Assert.IsNotNull(command, "Setup check: the click should have produced a real PromoteCommand.");

            scenario.DispatchTwice(command!);

            Assert.HasCount(1, red.InnerCircle.Where(c => c == discardCard), "The card should have been promoted exactly once, not twice.");
            Assert.AreEqual(0, red.VictoryPoints, "1 inner circle card / 3 = 0 VP, granted exactly once regardless.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Regression: PromoteCommand must disambiguate 2 copies of the same card id by
        // RuntimeId, not silently promote whichever copy Hand happens to contain (see
        // planning.txt section 1 / bug-log.md - found while reviewing this exact card). Real
        // CardFactory-created copies normally get DISTINCT Card.Id values (a random per-instance
        // suffix - see CardFactory.GenerateUniqueId), so this forces the rare suffix collision
        // this guards against rather than relying on one occurring naturally. ---

        [TestMethod]
        public void PlayVampire_DeclineWithTwoCopiesOfSameCardIdInHandAndDiscard_PromotesOnlyTheClickedDiscardCopy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var handCopy = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            var discardCopy = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            discardCopy.Id = handCopy.Id; // Force the rare Card.Id suffix collision this guards against.
            red.DeckManager.AddToDiscard(discardCopy);
            Assert.AreEqual(handCopy.Id, discardCopy.Id, "Setup check: both copies share the same (forced-collision) id.");
            Assert.AreNotEqual(handCopy.RuntimeId, discardCopy.RuntimeId, "Setup check: distinct physical copies have distinct RuntimeIds.");
            var card = scenario.GiveCard(PlayerColor.Red, "vampire");

            // No troops anywhere - straight to TargetingPromoteFromPile, no popup involved.
            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState);

            scenario.SelectPromoteFromPileCard(discardCopy); // Click the Discard copy specifically.

            Assert.Contains(discardCopy, red.InnerCircle.ToList(), "The clicked (Discard) copy should have been promoted.");
            Assert.DoesNotContain(handCopy, red.InnerCircle.ToList(), "The untouched Hand copy must NOT have been promoted instead.");
            Assert.Contains(handCopy, red.Hand.ToList(), "The Hand copy must remain exactly where it was.");
            Assert.DoesNotContain(discardCopy, red.DiscardPile.ToList());
        }
    }
}
