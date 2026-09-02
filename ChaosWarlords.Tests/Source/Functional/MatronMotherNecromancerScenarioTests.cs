using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Scenario-harness coverage for Matron Mother and Necromancer (planning.txt TIER 2 #2) -
    /// the first shipped use of EffectType.PromoteFromPile/ActionState.TargetingPromoteFromPile/
    /// CardLocation.HandOrDiscard (the immediate "promote a card from an expanded pool"
    /// primitive) and EffectType.MoveDeckToDiscard. Matron Mother ("Put your deck into your
    /// discard pile. Then promote a card from your discard pile.") chains PromoteFromPile off
    /// MoveDeckToDiscard's OnSuccess - NOT a choose-one. Necromancer ("Choose one: +3 Power.
    /// Promote this card, or a card from your hand or discard pile.") is a choose-one via
    /// IsOptional/Alternative, the same shape as Wight/Cultist of Myrkul. Runs the TIER 1 test
    /// matrix (planning.txt section 6.D) via the REAL "matron_mother"/"necromancer" cards.json
    /// entries, mirroring WightScenarioTests.cs's optional-effect accept/decline idiom and
    /// CraniumRatsScenarioTests.cs's wrong-player/stale-target/double-dispatch idiom.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MatronMotherNecromancerScenarioTests
    {
        // --- Matron Mother ---

        [TestMethod]
        public void PlayMatronMother_NonEmptyDeckAndHand_MovesDeckToDiscardThenPromotesChosenCard()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var matronMother = scenario.GiveCard(PlayerColor.Red, "matron_mother");
            var handCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard"); // Distinct from the real starting deck's cards.

            var deckCardsBefore = red.Deck.ToList();
            Assert.IsNotEmpty(deckCardsBefore, "Setup check: a real player starts with a non-empty deck.");

            scenario.PlayCard(matronMother);

            Assert.IsEmpty(red.Deck, "The entire draw pile should have moved to the discard pile.");
            foreach (var card in deckCardsBefore)
            {
                Assert.Contains(card, red.DiscardPile.ToList(), $"{card.Name} should now be in the discard pile.");
                Assert.AreEqual(CardLocation.DiscardPile, card.Location);
            }
            Assert.Contains(handCard, red.Hand.ToList(), "The hand card should be untouched by the deck-dump.");
            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState);

            var cardToPromote = red.DiscardPile.First();
            scenario.SelectPromoteFromPileCard(cardToPromote);

            Assert.Contains(cardToPromote, red.InnerCircle.ToList(), "The chosen card should have been promoted.");
            Assert.DoesNotContain(cardToPromote, red.DiscardPile.ToList());
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack, "No leftover effects should ambush the next card played.");
        }

        [TestMethod]
        public void PlayMatronMother_EmptyDeckAndEmptyDiscard_SkipsPromoteCleanly()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var matronMother = scenario.GiveCard(PlayerColor.Red, "matron_mother");
            scenario.GiveCard(PlayerColor.Red, "core_house_guard"); // Non-empty hand.
            red.DeckManager.Clear(); // Deck is empty when Matron Mother is played.
            Assert.IsEmpty(red.Deck);
            Assert.IsEmpty(red.DiscardPile, "Setup check: discard should also be empty for this scenario.");

            scenario.PlayCard(matronMother);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No valid Promote targets and no Alternative on this card - should skip cleanly back to Normal, not hang.");
            Assert.IsEmpty(red.InnerCircle, "Nothing should have been promoted.");
            Assert.IsEmpty(red.Deck, "Moving an empty deck to discard is a safe no-op.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayMatronMother_EmptyDeckButDiscardHasCardsFromPreviousTurns_StillOffersThoseForPromote()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var matronMother = scenario.GiveCard(PlayerColor.Red, "matron_mother");
            red.DeckManager.Clear(); // Deck is empty THIS turn...
            var oldDiscardCard = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            red.DeckManager.AddToDiscard(oldDiscardCard); // ...but discard already has a card from a previous turn.

            scenario.PlayCard(matronMother);

            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState, "A non-empty discard pile (even with an empty deck this turn) should still open the Promote window.");

            scenario.SelectPromoteFromPileCard(oldDiscardCard);

            Assert.Contains(oldDiscardCard, red.InnerCircle.ToList());
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void CancelTargeting_DuringPromoteFromPile_RestoresTheDumpedDeck_AndReturnsMatronMotherToHand()
        {
            // Same shape as CraniumRatsScenarioTests'
            // CancelTargeting_DuringOpponentDiscard_ReleasesTheForcedActor_AndReturnsTheCardToHand:
            // an automatic mutation (MoveDeckToDiscard) immediately followed by mandatory
            // targeting (PromoteFromPile). Both are now protected by the same
            // ActionSystem.EnsureTargetingSnapshot()/TryRestoreCardToHand(RuntimeId) fixes -
            // this exercises that machinery on the OTHER shipped "automatic mutation then
            // mandatory targeting" chain in the codebase, via the REAL ActionSystem.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var matronMother = scenario.GiveCard(PlayerColor.Red, "matron_mother");
            red.DeckManager.Clear(); // Start from a known, empty deck.
            var deckCard1 = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            var deckCard2 = scenario.CardDatabase.GetCardById("core_priestess", scenario.Context.Random)!;
            red.DeckManager.AddToTop(deckCard1);
            red.DeckManager.AddToTop(deckCard2);
            Assert.HasCount(2, red.Deck, "Setup check: deck now has exactly the two known cards.");
            Assert.IsEmpty(red.DiscardPile, "Setup check: discard starts empty.");

            scenario.PlayCard(matronMother);

            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState, "Setup check: mandatory Promote targeting should be open.");
            Assert.IsEmpty(red.Deck, "The deck should have been fully dumped to discard.");
            Assert.HasCount(2, red.DiscardPile, "Both known deck cards should now be in the discard pile.");

            scenario.Context.ActionSystem.CancelTargeting();

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            // Asserted by DefinitionId, not object reference: the snapshot restore rebuilds
            // Deck/DiscardPile/Hand with freshly-resolved Card instances via StateRestorer, and
            // the REAL CardDatabase this scenario harness uses re-randomizes Card.Id on every
            // CardFactory call - only DefinitionId/RuntimeId survive a restore unchanged (see
            // CraniumRatsScenarioTests' identically-reasoned cancel test).
            Assert.HasCount(2, red.Deck, "The draw pile must be restored to its pre-play contents after cancel.");
            CollectionAssert.AreEquivalent(
                new[] { deckCard1.DefinitionId, deckCard2.DefinitionId },
                red.Deck.Select(c => c.DefinitionId).ToList(),
                "The draw pile should contain exactly the original two cards, by DefinitionId.");
            Assert.IsEmpty(red.DiscardPile, "The discard pile must be back to its pre-play (empty) state - the dumped cards must not still be sitting there.");
            Assert.Contains(matronMother.DefinitionId, red.Hand.Select(c => c.DefinitionId).ToList(), "Matron Mother itself is restored to hand on any cancel, by id.");
        }

        [TestMethod]
        public void PlayMatronMother_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var matronMother = scenario.GiveCard(PlayerColor.Blue, "matron_mother"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(matronMother));

            Assert.Contains(matronMother, blue.Hand, "Matron Mother should still be in Blue's hand - the command must not have executed.");
            Assert.IsNotEmpty(blue.Deck, "Blue's deck must not have been dumped to discard by a rejected command.");
        }

        [TestMethod]
        public void PromoteCommand_ForMatronMother_TargetingNonexistentCard_IsRejected()
        {
            // Stale/nonexistent target (planning.txt matrix row 5): a forged/corrupted command
            // referencing a card id that isn't in Hand/Played/Discard at all.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var matronMother = scenario.GiveCard(PlayerColor.Red, "matron_mother");

            scenario.PlayCard(matronMother);
            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState, "Setup check: the real starting deck is non-empty, so Promote should be open.");

            scenario.AssertRejected(new PromoteCommand("this_card_id_does_not_exist", isChainedEffect: true));

            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState, "Still waiting for a real choice.");
            Assert.IsEmpty(red.InnerCircle);
        }

        [TestMethod]
        public void PromoteCommand_ForMatronMother_DispatchedTwice_SecondDispatchIsRejected()
        {
            // Double-dispatch/replay (planning.txt matrix row 7): re-sending the exact same
            // PromoteCommand after it already resolved must not promote a second card (or
            // re-promote the same one) - PromoteCommand.Validate() re-resolves the card from
            // Hand/Played/Discard, and it already left the discard pile after the first dispatch.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var matronMother = scenario.GiveCard(PlayerColor.Red, "matron_mother");
            scenario.PlayCard(matronMother);

            var cardToPromote = red.DiscardPile.First();
            var command = scenario.Context.ActionSystem.HandlePromoteFromPileSelection(cardToPromote);
            Assert.IsNotNull(command, "Setup check: the click should have produced a real PromoteCommand.");

            scenario.DispatchTwice(command!);

            Assert.HasCount(1, red.InnerCircle.Where(c => c == cardToPromote), "The card should have been promoted exactly once, not twice.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- Necromancer ---

        [TestMethod]
        public void PlayNecromancer_AcceptAndPromoteFromHand_MovesToInnerCircle_PowerUnchanged()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer");
            var handCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");

            scenario.PlayCard(necromancer);
            Assert.HasCount(1, scenario.Interactions, "Playing Necromancer should raise exactly one optional-effect popup.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(ActionState.TargetingPromoteFromPile, scenario.Context.ActionSystem.CurrentState);

            scenario.SelectPromoteFromPileCard(handCard);

            Assert.Contains(handCard, red.InnerCircle.ToList());
            Assert.DoesNotContain(handCard, red.Hand.ToList());
            Assert.AreEqual(0, red.Power, "Choose-one mutual exclusivity: accepting Promote must NOT also grant the +3 Power Alternative.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayNecromancer_AcceptAndPromoteFromDiscard_MovesToInnerCircle()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer");
            var discardCard = scenario.CardDatabase.GetCardById("core_house_guard", scenario.Context.Random)!;
            red.DeckManager.AddToDiscard(discardCard);

            scenario.PlayCard(necromancer);
            scenario.RespondToLatestInteraction(accept: true);

            scenario.SelectPromoteFromPileCard(discardCard);

            Assert.Contains(discardCard, red.InnerCircle.ToList());
            Assert.DoesNotContain(discardCard, red.DiscardPile.ToList());
            Assert.AreEqual(0, red.Power);
        }

        [TestMethod]
        public void PlayNecromancer_AcceptAndPromoteSelf_MovesNecromancerToInnerCircle()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer");

            scenario.PlayCard(necromancer);
            Assert.Contains(necromancer, red.PlayedCards.ToList(), "Setup check: the played card should be sitting in Played, findable as a promote target.");
            scenario.RespondToLatestInteraction(accept: true);

            scenario.SelectPromoteFromPileCard(necromancer);

            Assert.Contains(necromancer, red.InnerCircle.ToList(), "Necromancer should be able to promote itself.");
            Assert.DoesNotContain(necromancer, red.PlayedCards.ToList());
            Assert.AreEqual(0, red.Power);
        }

        [TestMethod]
        public void PlayNecromancer_DeclineOptionalEffect_GrantsThreePowerAlternative_NothingPromoted()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer");
            var handCard = scenario.GiveCard(PlayerColor.Red, "core_house_guard");

            scenario.PlayCard(necromancer);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(3, red.Power, "Declining should grant the +3 Power Alternative.");
            Assert.IsEmpty(red.InnerCircle, "Nothing should have been promoted.");
            Assert.Contains(handCard, red.Hand.ToList(), "Hand card should be untouched.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No targeting state should be left open.");
        }

        [TestMethod]
        public void PlayNecromancer_EmptyHandAndEmptyDiscard_AcceptingStillAllowsPromotingSelf()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer"); // Only card in hand.
            Assert.IsEmpty(red.DiscardPile, "Setup check: fresh player, no discard yet.");

            scenario.PlayCard(necromancer);
            Assert.HasCount(1, scenario.Interactions, "Even with an empty hand/discard, promoting itself is always a valid target, so the popup should still be requested.");
            scenario.RespondToLatestInteraction(accept: true);

            scenario.SelectPromoteFromPileCard(necromancer);

            Assert.Contains(necromancer, red.InnerCircle.ToList());
            Assert.AreEqual(0, red.Power);
        }

        [TestMethod]
        public void PromoteCommand_ForNecromancerPromoteFromPileFlow_DtoRoundTripsThroughRealDispatch()
        {
            // DTO round-trip (planning.txt matrix row 9) for the exact PromoteCommand shape
            // ActionSystem.HandlePromoteFromPileSelection produces (isChainedEffect: true).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer");

            scenario.PlayCard(necromancer);
            scenario.RespondToLatestInteraction(accept: true);

            var command = scenario.Context.ActionSystem.HandlePromoteFromPileSelection(necromancer);
            Assert.IsNotNull(command, "Setup check: the click should have produced a real PromoteCommand.");
            Assert.IsTrue(command!.IsChainedEffect, "Setup check: this flow should always produce isChainedEffect=true.");

            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as PromoteCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.CardId, hydrated!.CardId);
            Assert.IsTrue(hydrated.IsChainedEffect);

            scenario.Dispatch(hydrated);

            Assert.Contains(necromancer, red.InnerCircle.ToList(), "The hydrated command should have actually promoted the card through the real dispatch path.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        [TestMethod]
        public void PlayNecromancer_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var necromancer = scenario.GiveCard(PlayerColor.Blue, "necromancer"); // Belongs to Blue, not the active player.

            scenario.AssertRejected(new PlayCardCommand(necromancer));

            Assert.Contains(necromancer, blue.Hand, "Necromancer should still be in Blue's hand - the command must not have executed.");
            Assert.IsEmpty(scenario.Interactions, "No popup should have been raised for a rejected command.");
            Assert.AreEqual(0, blue.Power);
        }

        [TestMethod]
        public void PlayNecromancerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            // Double-dispatch/replay (planning.txt matrix row 7).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var necromancer = scenario.GiveCard(PlayerColor.Red, "necromancer");

            scenario.DispatchTwice(new PlayCardCommand(necromancer));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }
    }
}
