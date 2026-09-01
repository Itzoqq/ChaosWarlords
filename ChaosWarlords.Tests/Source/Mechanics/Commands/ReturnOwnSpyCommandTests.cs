using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for ReturnOwnSpyCommand - added as TIER 1 item 2
    // (planning.txt, test-hardening audit, 2026-09-01). See DiscardCardCommandTests.cs's own
    // doc comment for why this file exists now and not when the command was first built.
    [TestClass]
    [TestCategory("Unit")]
    public class ReturnOwnSpyCommandTests
    {
        private TestGameplayState _state = null!;
        private ChaosWarlords.Source.Entities.Actors.Player _player = null!;
        private Site _targetSite = null!;

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();
            _player = TestData.Players.RedPlayer();
            _state.TurnManager.ActivePlayer.Returns(_player);

            _targetSite = TestData.Sites.PowerCity();
            _targetSite.Id = 1;
            _state.MapManager.Sites.Returns(new List<Site> { _targetSite });
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenTargetSiteNotFound()
        {
            var command = new ReturnOwnSpyCommand(targetSiteId: 999);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_DelegatesToMapManager_CanReturnOwnSpy_ReturnsTrue()
        {
            _state.MapManager.CanReturnOwnSpy(_targetSite, _player).Returns(true);
            var command = new ReturnOwnSpyCommand(_targetSite.Id);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_DelegatesToMapManager_CanReturnOwnSpy_ReturnsFalse()
        {
            // e.g. the player has no spy at this site to return.
            _state.MapManager.CanReturnOwnSpy(_targetSite, _player).Returns(false);
            var command = new ReturnOwnSpyCommand(_targetSite.Id);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_WhenTargetSiteNotFound_DoesNothing()
        {
            var command = new ReturnOwnSpyCommand(targetSiteId: 999);

            command.Execute(_state.MatchContext);

            _state.MapManager.DidNotReceive().ReturnOwnSpy(Arg.Any<Site>(), Arg.Any<ChaosWarlords.Source.Entities.Actors.Player>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void Execute_WhenReturnOwnSpySucceeds_SetsPendingSiteForChain_AndCompletesAction()
        {
            _state.MapManager.ReturnOwnSpy(_targetSite, _player).Returns(true);
            var command = new ReturnOwnSpyCommand(_targetSite.Id);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.Received(1).SetPendingSiteForChain(_targetSite);
            _state.ActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_WhenReturnOwnSpyFails_DoesNotSetPendingSiteOrCompleteAction()
        {
            // MapManager.ReturnOwnSpy re-checks CanReturnOwnSpy internally and can fail even
            // past Validate() if state changed between validation and execution.
            _state.MapManager.ReturnOwnSpy(_targetSite, _player).Returns(false);
            var command = new ReturnOwnSpyCommand(_targetSite.Id);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceive().SetPendingSiteForChain(Arg.Any<Site>());
            _state.ActionSystem.DidNotReceive().CompleteAction();
        }
    }
}
