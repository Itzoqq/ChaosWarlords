using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using System.Text.Json;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class PromoteCommandTests
    {
        private TestGameplayState _state = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
        }

        [TestMethod]
        public void Validate_Returns_False_When_CardNotInHandOrPlayed()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand("missing_card");

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Validate_Returns_True_When_CardInHand()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Validate_Returns_True_When_CardInPlayedCards()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InPlayed().Build();
            var player = TestData.Players.RedPlayer();
            player.AddToPlayed(card);
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Validate_DoesNotMutateState_CardRemainsInHandAfterValidation()
        {
            // Arrange: regression test - Validate() used to call TryPromoteCard() directly, which
            // actually moved the card to the Inner Circle as a side effect of "checking". Since
            // CommandDispatcher calls Validate() then Execute() on the same instance, that left
            // Execute()'s own promotion call unable to find the card at all. Validate() must be a
            // pure read (see IGameCommand.Validate's contract).
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            command.Validate(_state.MatchContext);

            // Assert
            CollectionAssert.Contains(player.Hand.ToList(), card, "Validate() must not remove the card from Hand");
            CollectionAssert.DoesNotContain(player.InnerCircle.ToList(), card, "Validate() must not promote the card");
        }

        [TestMethod]
        public void Execute_MovesCardToInnerCircle_AndRecordsAction_WhenCardInHand()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            CollectionAssert.DoesNotContain(player.Hand.ToList(), card);
            CollectionAssert.Contains(player.InnerCircle.ToList(), card);
        }

        [TestMethod]
        public void Execute_FollowingValidate_StillPromotesTheCard()
        {
            // Arrange: exercises the same Validate()-then-Execute() sequence CommandDispatcher
            // uses in production, guarding against the double-invocation bug described above.
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id);

            // Act
            var isValid = command.Validate(_state.MatchContext);
            command.Execute(_state.MatchContext);

            // Assert
            Assert.IsTrue(isValid);
            CollectionAssert.Contains(player.InnerCircle.ToList(), card);
        }

        [TestMethod]
        public void Execute_DoesNothing_When_CardNotFound()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand("missing_card");

            // Act & Assert: should not throw
            command.Execute(_state.MatchContext);

            Assert.IsEmpty(player.InnerCircle);
        }

        [TestMethod]
        public void Validate_WithIsChainedEffectTrue_ReturnsTrue_WhenCardOnlyInDiscardPile()
        {
            // Arrange: the new EffectType.PromoteFromPile pool (Matron Mother/Necromancer)
            // widens Validate()'s lookup to include the discard pile - but only for the
            // immediate chained flow (IsChainedEffect == true). The legacy 1-arg/false form
            // must never reach Discard - see the regression guard below.
            var card = new CardBuilder().WithName("card1").InDiscard().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInDiscard(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id, isChainedEffect: true);

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Execute_WithIsChainedEffectTrue_MovesCardToInnerCircle_WhenCardOnlyInDiscardPile()
        {
            // Arrange
            var card = new CardBuilder().WithName("card1").InDiscard().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInDiscard(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id, isChainedEffect: true);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            CollectionAssert.DoesNotContain(player.DiscardPile.ToList(), card);
            CollectionAssert.Contains(player.InnerCircle.ToList(), card);
        }

        [TestMethod]
        public void Validate_WithIsChainedEffectDefaultOmitted_ReturnsFalse_WhenCardOnlyInDiscardPile()
        {
            // Regression guard for the tightened discard-lookup scope: the legacy deferred
            // end-of-turn promotion-credit flow (EffectType.Promote, redeemed via
            // PromoteInputMode) constructs PromoteCommand with the 1-arg ctor, relying on
            // IsChainedEffect defaulting to false - that flow must NEVER be able to reach into
            // Discard, even though the newer chained PromoteFromPile flow legitimately can (see
            // the two tests above). Before this fix, Validate()/Execute() searched Discard
            // unconditionally regardless of IsChainedEffect - a real overpermission gap.
            var card = new CardBuilder().WithName("card1").InDiscard().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInDiscard(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id); // isChainedEffect intentionally omitted.

            // Act
            var result = command.Validate(_state.MatchContext);

            // Assert
            Assert.IsFalse(result, "The legacy (non-chained) flow must not be able to promote a card that only lives in Discard.");
        }

        [TestMethod]
        public void Execute_WithIsChainedEffectTrue_CallsActionSystemCompleteAction()
        {
            // Arrange: the shape ActionSystem.HandlePromoteFromPileSelection produces for the
            // NEW immediate EffectType.PromoteFromPile flow (Matron Mother, Necromancer) - the
            // blocking EffectContext on the ExecutionStack must be resolved when this command
            // finishes.
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id, isChainedEffect: true);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_WithIsChainedEffectDefaultOmitted_DoesNotCallActionSystemCompleteAction()
        {
            // Regression guard (the single most important test in this file): the LEGACY
            // deferred end-of-turn promotion-credit flow (Noble/Cultist of Myrkul, redeemed via
            // PromoteInputMode) constructs PromoteCommand with the 1-arg ctor, relying on
            // IsChainedEffect defaulting to false. By the time that command runs, ActionSystem's
            // ExecutionStack is already empty - calling CompleteAction() here would incorrectly
            // pop/complete an unrelated stack entry or fire OnActionCompleted prematurely. A
            // wrong default here would silently corrupt that already-shipped behavior.
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id); // isChainedEffect intentionally omitted.

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_WithIsChainedEffectExplicitFalse_DoesNotCallActionSystemCompleteAction()
        {
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(card.Id, isChainedEffect: false);

            // Act
            command.Execute(_state.MatchContext);

            // Assert
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void ToDto_ThenHydrate_RoundTripsIsChainedEffect_WhenTrue()
        {
            // Arrange
            var original = new PromoteCommand("card1", isChainedEffect: true);
            var dto = original.ToDto();

            // Act
            var hydrated = CommandHydrator.HydrateCommand(dto, _state.MatchContext) as PromoteCommand;

            // Assert
            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.CardId, hydrated!.CardId);
            Assert.IsTrue(hydrated.IsChainedEffect);
        }

        [TestMethod]
        public void ToDto_ThenHydrate_RoundTripsIsChainedEffect_WhenFalse()
        {
            // Arrange
            var original = new PromoteCommand("card1"); // Legacy shape.
            var dto = original.ToDto();

            // Act
            var hydrated = CommandHydrator.HydrateCommand(dto, _state.MatchContext) as PromoteCommand;

            // Assert
            Assert.IsNotNull(hydrated);
            Assert.IsFalse(hydrated!.IsChainedEffect);
        }

        [TestMethod]
        public void PromoteCommandDto_ConstructedWithoutSettingIsChainedEffect_HydratesAsFalse()
        {
            // Arrange: simulates an old-shaped DTO built by code that predates this field.
            var dto = new PromoteCommandDto { CardId = "card1" }; // IsChainedEffect never set.

            // Act
            var hydrated = CommandHydrator.HydrateCommand(dto, _state.MatchContext) as PromoteCommand;

            // Assert
            Assert.IsNotNull(hydrated);
            Assert.IsFalse(hydrated!.IsChainedEffect);
        }

        [TestMethod]
        public void PromoteCommandDto_DeserializedFromJsonMissingIsChainedEffectKey_HydratesAsFalse()
        {
            // Arrange: a real old replay/network JSON payload recorded before IsChainedEffect
            // existed - the key is entirely absent, not just false.
            var json = "{\"t\":\"promote\",\"Seq\":1,\"Seat\":0,\"CardId\":\"card1\"}";
            var dto = JsonSerializer.Deserialize<GameCommandDto>(json);
            Assert.IsInstanceOfType(dto, typeof(PromoteCommandDto), "Setup check: discriminator should resolve to PromoteCommandDto.");

            // Act
            var hydrated = CommandHydrator.HydrateCommand(dto!, _state.MatchContext) as PromoteCommand;

            // Assert
            Assert.IsNotNull(hydrated);
            Assert.AreEqual("card1", hydrated!.CardId);
            Assert.IsFalse(hydrated.IsChainedEffect, "A missing key must deserialize to the old (only) behavior, not throw or default to true.");
        }

        // --- CardRuntimeId: disambiguating 2 copies of the same card definition ---

        [TestMethod]
        public void Execute_WithTwoCopiesOfSameCardId_PromotesOnlyTheOneMatchingCardRuntimeId()
        {
            // Regression test for the bug this constructor overload fixes: before
            // CardRuntimeId existed, ResolveCard's Id-only lookup checked Hand before Discard,
            // so clicking the Discard copy in a PromoteFromPile browser would have silently
            // promoted the Hand copy instead.
            var handCopy = new CardBuilder().WithName("card1").InHand().Build();
            var discardCopy = new CardBuilder().WithName("card1").InDiscard().Build();
            Assert.AreEqual(handCopy.Id, discardCopy.Id, "Setup check: both copies share the same definitional id.");
            Assert.AreNotEqual(handCopy.RuntimeId, discardCopy.RuntimeId, "Setup check: distinct physical copies have distinct RuntimeIds.");
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(handCopy).WithCardsInDiscard(discardCopy).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(discardCopy, isChainedEffect: true); // Simulates clicking the Discard copy.

            command.Execute(_state.MatchContext);

            CollectionAssert.Contains(player.InnerCircle.ToList(), discardCopy, "The clicked (Discard) copy should have been promoted.");
            CollectionAssert.DoesNotContain(player.InnerCircle.ToList(), handCopy, "The untouched Hand copy must NOT have been promoted instead.");
            CollectionAssert.Contains(player.Hand.ToList(), handCopy, "The Hand copy must remain exactly where it was.");
            CollectionAssert.DoesNotContain(player.DiscardPile.ToList(), discardCopy);
        }

        [TestMethod]
        public void Execute_ReplayedAfterItsOwnRuntimeIdTargetAlreadyPromoted_DoesNotFallBackToASameIdSibling()
        {
            // Compound-scenario regression: when CardRuntimeId is set, ResolveCard must resolve
            // BY RUNTIMEID ONLY - it must NOT fall back to a CardId match once that exact
            // physical copy is gone (e.g. a replayed/double-dispatched command after the first
            // dispatch already promoted it). Falling back there would silently re-target a
            // different, same-Id sibling instead of correctly finding nothing left to do -
            // exactly the ambiguity CardRuntimeId exists to prevent.
            var promotedCopy = new CardBuilder().WithName("card1").InHand().Build();
            var siblingCopy = new CardBuilder().WithName("card1").InDiscard().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInHand(promotedCopy).WithCardsInDiscard(siblingCopy).Build();
            _state.TurnManager.ActivePlayer.Returns(player);
            var command = new PromoteCommand(promotedCopy, isChainedEffect: true);

            command.Execute(_state.MatchContext); // First dispatch: promotes promotedCopy.
            Assert.Contains(promotedCopy, player.InnerCircle.ToList(), "Setup check: the first dispatch should have promoted promotedCopy.");

            command.Execute(_state.MatchContext); // Replayed/double-dispatched second call of the SAME command.

            CollectionAssert.DoesNotContain(player.InnerCircle.ToList(), siblingCopy, "A same-Id sibling elsewhere must NOT be silently promoted once the RuntimeId target is already gone.");
            Assert.HasCount(1, player.InnerCircle, "Exactly one promotion total, not two.");
            CollectionAssert.Contains(player.DiscardPile.ToList(), siblingCopy, "The untouched sibling must remain exactly where it was.");
        }

        [TestMethod]
        public void CardBasedConstructor_CapturesBothCardIdAndCardRuntimeId()
        {
            var card = new CardBuilder().WithName("card1").InHand().Build();

            var command = new PromoteCommand(card, isChainedEffect: true);

            Assert.AreEqual(card.Id, command.CardId);
            Assert.AreEqual(card.RuntimeId, command.CardRuntimeId);
        }

        [TestMethod]
        public void StringConstructor_LeavesCardRuntimeIdNull()
        {
            // The legacy/string-only construction path (every pre-existing call site) must be
            // completely unaffected - CardRuntimeId defaults to null, and ResolveCard falls
            // back to the plain CardId lookup exactly as it always did.
            var command = new PromoteCommand("card1");

            Assert.IsNull(command.CardRuntimeId);
        }

        [TestMethod]
        public void ToDto_ThenHydrate_RoundTripsCardRuntimeId()
        {
            var card = new CardBuilder().WithName("card1").InHand().Build();
            var original = new PromoteCommand(card, isChainedEffect: true);

            var dto = original.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, _state.MatchContext) as PromoteCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.CardRuntimeId, hydrated!.CardRuntimeId);
        }

        [TestMethod]
        public void PromoteCommandDto_DeserializedFromJsonMissingCardRuntimeIdKey_HydratesWithNullCardRuntimeId()
        {
            // A real old replay/network JSON payload recorded before CardRuntimeId existed -
            // the key is entirely absent, not just null. Must still hydrate and resolve by
            // CardId alone, not throw or reject the command.
            var json = "{\"t\":\"promote\",\"Seq\":1,\"Seat\":0,\"CardId\":\"card1\",\"IsChainedEffect\":true}";
            var dto = JsonSerializer.Deserialize<GameCommandDto>(json);
            Assert.IsInstanceOfType(dto, typeof(PromoteCommandDto));

            var hydrated = CommandHydrator.HydrateCommand(dto!, _state.MatchContext) as PromoteCommand;

            Assert.IsNotNull(hydrated);
            Assert.IsNull(hydrated!.CardRuntimeId, "A missing key must deserialize to null, not throw or default to some other value.");

            // And the fallback-to-CardId resolution must still actually work end-to-end.
            var card = new CardBuilder().WithName("card1").InDiscard().Build();
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithCardsInDiscard(card).Build();
            _state.TurnManager.ActivePlayer.Returns(player);

            hydrated.Execute(_state.MatchContext);

            CollectionAssert.Contains(player.InnerCircle.ToList(), card);
        }
    }
}
