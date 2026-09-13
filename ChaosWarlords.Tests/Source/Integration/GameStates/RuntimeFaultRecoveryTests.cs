using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Integration.GameStates
{
    [TestClass]
    [TestCategory("Unit")]
    public class RuntimeFaultRecoveryTests
    {
        private ICrashReporter _crashReporter = null!;
        private IReplayManager _replayManager = null!;
        private IStateManager _stateManager = null!;
        private IGameLogger _logger = null!;
        private IState _mainMenu = null!;
        private int _exitCount;

        [TestInitialize]
        public void Setup()
        {
            _crashReporter = Substitute.For<ICrashReporter>();
            _replayManager = Substitute.For<IReplayManager>();
            _stateManager = Substitute.For<IStateManager>();
            _logger = Substitute.For<IGameLogger>();
            _mainMenu = Substitute.For<IState>();
        }

        [TestMethod]
        public void Recover_FrameException_ReportsCrashAndTransitionsToFreshMainMenu()
        {
            var recovery = CreateRecovery();
            var exception = new InvalidOperationException("boom");

            recovery.Recover(exception, "Update");

            _crashReporter.Received(1).ReportCrash(exception, _replayManager, "Update");
            _stateManager.Received(1).ChangeState(_mainMenu);
            Assert.AreEqual(0, _exitCount);
        }

        [TestMethod]
        public void Recover_FrameException_UnloadsTheFailedStateThroughNormalStateLifecycle()
        {
            var stateManager = new StateManager(null!);
            var failedState = Substitute.For<IState>();
            var mainMenu = Substitute.For<IState>();
            stateManager.PushState(failedState);
            var recovery = new RuntimeFaultRecovery(
                _crashReporter,
                _replayManager,
                stateManager,
                () => mainMenu,
                () => _exitCount++,
                _logger);

            recovery.Recover(new InvalidOperationException("gameplay failed"), "Update");

            failedState.Received(1).UnloadContent();
            Assert.AreSame(mainMenu, stateManager.GetCurrentState());
            Assert.AreEqual(0, _exitCount);
        }

        [TestMethod]
        public void Recover_WhenMainMenuTransitionThrows_ExitsInsteadOfResumingCorruptedState()
        {
            _stateManager.When(manager => manager.ChangeState(Arg.Any<IState>()))
                .Do(_ => throw new InvalidOperationException("menu load failed"));
            var recovery = CreateRecovery();

            recovery.Recover(new InvalidOperationException("gameplay failed"), "Draw");

            Assert.AreEqual(1, _exitCount);
            _logger.Received(1).Log(Arg.Is<string>(message => message.Contains("Draw")), LogChannel.Error);
        }

        [TestMethod]
        public void Recover_WhenMainMenuFactoryThrows_ExitsWithoutAttemptingStateTransition()
        {
            var recovery = CreateRecovery(() => throw new InvalidOperationException("menu factory failed"));

            recovery.Recover(new InvalidOperationException("gameplay failed"), "Update");

            Assert.AreEqual(1, _exitCount);
            _stateManager.DidNotReceive().ChangeState(Arg.Any<IState>());
        }

        private RuntimeFaultRecovery CreateRecovery(Func<IState>? mainMenuFactory = null)
        {
            return new RuntimeFaultRecovery(
                _crashReporter,
                _replayManager,
                _stateManager,
                mainMenuFactory ?? (() => _mainMenu),
                () => _exitCount++,
                _logger);
        }
    }
}
