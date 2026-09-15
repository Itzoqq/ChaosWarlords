using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for ReturnSpyToPlaceCommand - the "return one of
    // your own spies first" half of rulebook p.12's empty-barracks Place-a-Spy exception (see
    // planning.txt TIER 1 item 17, SpySubsystem.HandlePlaceSpy).
    [TestClass]
    [TestCategory("Unit")]
    public class ReturnSpyToPlaceCommandTests
    {
        private TestGameplayState _state = null!;
        private ChaosWarlords.Source.Entities.Actors.Player _player = null!;
        private Site _targetSite = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _player = TestData.Players.RedPlayer();
            _player.SpiesInBarracks = 0;
            _state.TurnManager.ActivePlayer.Returns(_player);

            _targetSite = TestData.Sites.PowerCity();
            _targetSite.Id = 1;
            _state.MapManager.Sites.Returns(new List<Site> { _targetSite });

            // Same authorization shape as PlaceSpyCommand/ReturnOwnSpyCommand - only ever
            // legitimately dispatched while a Place Spy effect is the pending targeting state.
            _state.ActionSystem.CurrentState.Returns(ActionState.TargetingPlaceSpy);
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetSiteNotFound()
        {
            var command = new ReturnSpyToPlaceCommand(targetSiteId: 999);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNoPlaceSpyEffectIsPending()
        {
            // Adversarial: this is never a standalone basic action - a directly-dispatched
            // attempt outside its owning targeting state must be rejected even when every
            // other check would otherwise pass.
            _state.ActionSystem.CurrentState.Returns(ActionState.Normal);
            _state.MapManager.CanReturnOwnSpy(_targetSite, _player).Returns(true);
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenBarracksIsNotEmpty()
        {
            // Adversarial: re-derived from live player state, not trusted from the caller -
            // once the barracks has a spy, the click must build a PlaceSpyCommand instead.
            _player.SpiesInBarracks = 1;
            _state.MapManager.CanReturnOwnSpy(_targetSite, _player).Returns(true);
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNoOwnSpyAtSite()
        {
            _state.MapManager.CanReturnOwnSpy(_targetSite, _player).Returns(false);
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenBarracksEmptyAndOwnSpyAtSite()
        {
            _state.MapManager.CanReturnOwnSpy(_targetSite, _player).Returns(true);
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_WhenTargetSiteNotFound_DoesNothing()
        {
            var command = new ReturnSpyToPlaceCommand(targetSiteId: 999);

            command.Execute(_state.MatchContext);

            _state.MapManager.DidNotReceive().ReturnOwnSpy(Arg.Any<Site>(), Arg.Any<ChaosWarlords.Source.Entities.Actors.Player>());
        }

        [TestMethod]
        public void Execute_WhenReturnSucceeds_DoesNotCompleteAction()
        {
            // The pending PlaceSpy effect is still open - only the follow-up placement click
            // (an ordinary PlaceSpyCommand, once the barracks has a spy again) completes it.
            _state.MapManager.ReturnOwnSpy(_targetSite, _player).Returns(true);
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id);

            command.Execute(_state.MatchContext);

            _state.MapManager.Received(1).ReturnOwnSpy(_targetSite, _player);
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_WhenReturnFails_DoesNotCompleteAction()
        {
            _state.MapManager.ReturnOwnSpy(_targetSite, _player).Returns(false);
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void ToDto_ThenHydrate_RoundTripsSiteIdAndCardId()
        {
            var command = new ReturnSpyToPlaceCommand(_targetSite.Id, "drow_spy_master");

            var dto = command.ToDto();
            var hydrated = CommandHydrator.HydrateCommand(dto, _state.MatchContext) as ReturnSpyToPlaceCommand;

            Assert.IsNotNull(hydrated);
            Assert.AreEqual(command.TargetSiteId, hydrated!.TargetSiteId);
            Assert.AreEqual(command.CardId, hydrated.CardId);
        }
    }
}
