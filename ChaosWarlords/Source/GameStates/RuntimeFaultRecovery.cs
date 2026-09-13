using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.GameStates
{
    /// <summary>
    /// Reports an unhandled frame exception and replaces the active state with a clean main menu.
    /// </summary>
    public sealed class RuntimeFaultRecovery
    {
        private readonly ICrashReporter _crashReporter;
        private readonly IReplayManager _replayManager;
        private readonly IStateManager _stateManager;
        private readonly Func<IState> _mainMenuFactory;
        private readonly Action _exitApplication;
        private readonly IGameLogger _logger;

        public RuntimeFaultRecovery(
            ICrashReporter crashReporter,
            IReplayManager replayManager,
            IStateManager stateManager,
            Func<IState> mainMenuFactory,
            Action exitApplication,
            IGameLogger logger)
        {
            _crashReporter = crashReporter ?? throw new ArgumentNullException(nameof(crashReporter));
            _replayManager = replayManager ?? throw new ArgumentNullException(nameof(replayManager));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _mainMenuFactory = mainMenuFactory ?? throw new ArgumentNullException(nameof(mainMenuFactory));
            _exitApplication = exitApplication ?? throw new ArgumentNullException(nameof(exitApplication));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Recover(Exception exception, string source)
        {
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentException.ThrowIfNullOrWhiteSpace(source);

            _crashReporter.ReportCrash(exception, _replayManager, source);

            try
            {
                _stateManager.ChangeState(_mainMenuFactory());
            }
            catch (Exception recoveryException)
            {
                _logger.Log($"Unable to recover from {source} fault; exiting application.", LogChannel.Error);
                _logger.Log(recoveryException, LogChannel.Error);
                _exitApplication();
            }
        }
    }
}
