using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for ResolveSpyCommand (the base "Return an enemy
    // spy" 3-Power action, and EffectType.ReturnEnemySpy's card-funded free version - Red
    // Dragon). Mirrors ReturnAnySpyCommandTests.cs's shape - both commands share the identical
    // Power-cost/CurrentState-authorization gaps fixed under planning.txt TIER 1 item 15.
    [TestClass]
    [TestCategory("Unit")]
    public class ResolveSpyCommandTests
    {
        private TestGameplayState _state = null!;
        private Player _player = null!;
        private Site _site = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _player = new PlayerBuilder().WithColor(PlayerColor.Red).WithPower(5).Build();
            _state.TurnManager.ActivePlayer.Returns(_player);

            _site = TestData.Sites.NeutralSite();
            _site.Id = 10;
            _site.AddSpy(PlayerColor.Blue);
            _state.MapManager.Sites.Returns(new List<Site> { _site });

            // This command's only 2 genuine entry points (SpySubsystem.
            // HandleReturnSpyInitialClick for a single enemy spy, and ActionSystem.
            // FinalizeSpyReturn's disambiguation branch for 2+) both leave CurrentState at
            // TargetingReturnSpy or SelectingSpyToReturn - see Validate's own CurrentState gate,
            // planning.txt TIER 1 item 15. Configured here so every other test in this file
            // doesn't have to repeat it.
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingReturnSpy);
        }

        [TestMethod]
        public void Execute_CallsFinalizeSpyReturnOnActionSystem()
        {
            _state.MapManager.ReturnSpecificSpy(_site, _player, PlayerColor.Blue).Returns(true);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            command.Execute(_state.MatchContext);

            _state.MapManager.Received(1).ReturnSpecificSpy(_site, _player, PlayerColor.Blue);
        }

        // --- Validate() must re-derive the enemy-only restriction, not trust the caller (Red
        // Dragon: "Return an enemy spy" is this command's first non-base-action consumer) ---

        [TestMethod]
        public void Validate_ReturnsFalse_WhenSpyColorIsTheActivePlayersOwn()
        {
            _site.AddSpy(_player.Color);
            var command = new ResolveSpyCommand(10, _player.Color);

            var result = command.Validate(_state.MatchContext);

            Assert.IsFalse(result, "A forged command naming the active player's OWN spy color must be rejected - use ReturnOwnSpyCommand instead.");
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenSpyColorIsAnEnemys()
        {
            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            var result = command.Validate(_state.MatchContext);

            Assert.IsTrue(result, "An enemy's spy color must still validate successfully - only the active player's own color is rejected.");
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNoReturnSpyEffectIsPending()
        {
            // Adversarial: this command is never a standalone free action outside its owning
            // targeting state - a directly-dispatched attempt on an ordinary turn (CurrentState
            // still Normal) must be rejected even though the site/spy/color are all otherwise
            // legitimate. planning.txt TIER 1 item 15.
            _state.ActionSystem.CurrentState.Returns(ActionState.Normal);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenDisambiguatingBetween2PlusEnemySpies()
        {
            // ActionSystem.FinalizeSpyReturn's non-ReturnUnitOrSpy branch leaves CurrentState at
            // SelectingSpyToReturn (set by TransitionToSpySelection) rather than restoring
            // TargetingReturnSpy - this is still a genuine pending "return an enemy spy" effect.
            _state.ActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenSelectingSpyToReturnIsOwnedByReturnUnitOrSpy()
        {
            // Adversarial: SelectingSpyToReturn is shared with EffectType.ReturnUnitOrSpy's own
            // disambiguation (Intellect Devourer) - that flow's real command is
            // ReturnAnySpyCommand, not this one. A forged ResolveSpyCommand dispatched during
            // that disambiguation must still be rejected.
            _state.ActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            _state.ActionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.ReturnUnitOrSpy, 1));
            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_ReturningAnEnemySpyWithoutACard_SpendsPowerAndCompletesAction()
        {
            _state.MapManager.ReturnSpecificSpy(_site, _player, PlayerColor.Blue).Returns(true);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue, cardId: null);

            command.Execute(_state.MatchContext);

            Assert.AreEqual(5 - GameConstants.ReturnSpyPowerCost, _player.Power);
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_ReturningAnEnemySpyFundedByACard_SpendsNoPower()
        {
            // "Funded by a card" is only trusted while ActionSystem's own CurrentSourceEffect
            // agrees a real ReturnEnemySpy effect is pending - a bare CardId string alone is
            // never enough (planning.txt TIER 1 item 15).
            _state.ActionSystem.CurrentSourceEffect.Returns(new CardEffect(EffectType.ReturnEnemySpy, 1));
            _state.MapManager.ReturnSpecificSpy(_site, _player, PlayerColor.Blue).Returns(true);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue, cardId: "red_dragon_abc123");

            command.Execute(_state.MatchContext);

            Assert.AreEqual(5, _player.Power, "Card-funded return must not spend Power.");
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_ForgedCardIdWithNoGenuinePendingEffect_StillRequiresAndSpendsPower()
        {
            // Adversarial: a directly-dispatched command can't waive the Power cost just by
            // naming a non-empty CardId when ActionSystem has no real ReturnEnemySpy effect
            // pending. CurrentSourceEffect deliberately left unconfigured (null).
            _state.MapManager.ReturnSpecificSpy(_site, _player, PlayerColor.Blue).Returns(true);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue, cardId: "forged_card_id");

            command.Execute(_state.MatchContext);

            Assert.AreEqual(5 - GameConstants.ReturnSpyPowerCost, _player.Power, "A forged CardId with no real pending effect must not waive the Power cost.");
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_InsufficientPowerAndNotCardFunded_NotifiesFailureWithoutMutatingTheBoard()
        {
            // Previously affordability was never checked before mutating the board at all, so
            // a directly-dispatched command with insufficient Power still returned the spy for
            // free. planning.txt TIER 1 item 15.
            var poorPlayer = new PlayerBuilder().WithColor(PlayerColor.Red).WithPower(0).Build();
            _state.TurnManager.ActivePlayer.Returns(poorPlayer);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue, cardId: null);

            command.Execute(_state.MatchContext);

            _state.MapManager.DidNotReceive().ReturnSpecificSpy(Arg.Any<Site>(), Arg.Any<Player>(), Arg.Any<PlayerColor>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
            _state.ActionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        [TestMethod]
        public void Execute_WhenReturnSpecificSpyFails_NotifiesFailureAndDoesNotCompleteAction()
        {
            _state.MapManager.ReturnSpecificSpy(_site, _player, PlayerColor.Blue).Returns(false);
            var command = new ResolveSpyCommand(10, PlayerColor.Blue);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceive().CompleteAction();
            _state.ActionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }
    }
}
