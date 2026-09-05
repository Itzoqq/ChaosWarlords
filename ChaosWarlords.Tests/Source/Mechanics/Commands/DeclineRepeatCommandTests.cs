using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Data.Enums;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    /// <summary>
    /// Unit-level coverage for DeclineRepeatCommand (CardEffect.AllowPartialRepeat's "stop
    /// early, keep whatever already resolved" primitive - see Council Member: "Move up to 2
    /// enemy troops"). CouncilMemberScenarioTests.cs covers the same primitive end to end
    /// through the real CommandDispatcher/cards.json path - this file isolates Validate()'s
    /// individual gates and the DTO round-trip (standing test matrix row 9).
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class DeclineRepeatCommandTests
    {
        private TestGameplayState _state = null!;
        private Card _card = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _card = new Card("council_member", "Council Member", 6, CardAspect.Blasphemy, 3, 6, 0);
        }

        private EffectContext BuildEffectContext(ActionState state, bool allowPartialRepeat)
        {
            var sourceEffect = new CardEffect(EffectType.MoveUnit, 2) { AllowPartialRepeat = allowPartialRepeat };
            return new EffectContext(state, _card, requiresInput: true, "Effect: MoveUnit", onResolved: _ => { }, sourceEffect: sourceEffect);
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNoEffectIsPending()
        {
            _state.ActionSystem.CurrentEffect.Returns((EffectContext?)null);
            var command = new DeclineRepeatCommand(_card.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenEffectDoesNotAllowPartialRepeat()
        {
            // Deathblade's mandatory "Assassinate 2 troops" shape - AllowPartialRepeat is
            // false, so declining early must never be possible for it.
            var effect = BuildEffectContext(ActionState.TargetingMoveSource, allowPartialRepeat: false);
            _state.ActionSystem.CurrentEffect.Returns(effect);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingMoveSource);
            var command = new DeclineRepeatCommand(_card.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenAllowPartialRepeatIsSetOnANonRepeatCapableEffectType()
        {
            // Defense-in-depth against a future card mistakenly (or maliciously) setting
            // AllowPartialRepeat on an effect type whose strategy never opted into
            // SupportsRepeat (PlaceSpyStrategy, here) - without this guard, RemainingRepeats
            // would be stuck at its default of 1, so CurrentState == effect.EffectType would
            // already be true at the very entry state, before any real target was ever picked.
            var sourceEffect = new CardEffect(EffectType.PlaceSpy, 1) { AllowPartialRepeat = true };
            var effect = new EffectContext(ActionState.TargetingPlaceSpy, _card, requiresInput: true, "Effect: PlaceSpy", onResolved: _ => { }, sourceEffect: sourceEffect);
            _state.ActionSystem.CurrentEffect.Returns(effect);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingPlaceSpy);
            var command = new DeclineRepeatCommand(_card.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenCardIdDoesNotMatchThePendingEffectsSourceCard()
        {
            var effect = BuildEffectContext(ActionState.TargetingMoveSource, allowPartialRepeat: true);
            _state.ActionSystem.CurrentEffect.Returns(effect);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingMoveSource);
            var command = new DeclineRepeatCommand("some_other_card");

            Assert.IsFalse(command.Validate(_state.MatchContext), "A stale/forged command referencing a different card's sequence must be rejected.");
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNotAtAGenuineRepeatBoundary()
        {
            // MoveUnit's own 2nd ActionState (source picked, destination not yet chosen) -
            // must not be declinable mid-sub-target.
            var effect = BuildEffectContext(ActionState.TargetingMoveSource, allowPartialRepeat: true);
            _state.ActionSystem.CurrentEffect.Returns(effect);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingMoveDestination);
            var command = new DeclineRepeatCommand(_card.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenPartialRepeatAllowedCardMatchesAndAtTheEntryState()
        {
            var effect = BuildEffectContext(ActionState.TargetingMoveSource, allowPartialRepeat: true);
            _state.ActionSystem.CurrentEffect.Returns(effect);
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingMoveSource);
            var command = new DeclineRepeatCommand(_card.Id);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_CallsActionSystem_DeclineRemainingRepeats()
        {
            var command = new DeclineRepeatCommand(_card.Id);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.Received(1).DeclineRemainingRepeats();
        }

        [TestMethod]
        public void ToDto_CarriesCardId()
        {
            var command = new DeclineRepeatCommand("council_member");

            var dto = (DeclineRepeatCommandDto)command.ToDto();

            Assert.AreEqual("council_member", dto.CardId);
            Assert.AreEqual(CommandType.DeclineRepeat, command.Type);
        }

        [TestMethod]
        public void HydrateCommand_RoundTripsToAnEquivalentCommand()
        {
            var original = new DeclineRepeatCommand("council_member");
            var dto = original.ToDto();

            var hydrated = DtoMapper.HydrateCommand(dto, _state.MatchContext) as DeclineRepeatCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(original.CardId, hydrated!.CardId);
        }
    }
}
