using System;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    public class CommandDispatcher : ICommandDispatcher
    {
        private readonly IReplayManager _replayManager;
        private readonly IGameLogger _logger;
        private int _localSequenceCounter;

        public CommandDispatcher(IReplayManager replayManager, IGameLogger logger)
        {
            _replayManager = replayManager ?? throw new ArgumentNullException(nameof(replayManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Dispatch(IGameCommand command, IGameplayState state)
        {
            try
            {
                // Record the command for replay (unless we're currently replaying)
                if (!_replayManager.IsReplaying)
                {
                    state.MatchContext.RecordAction(command.GetType().Name, command.ToString() ?? "Command");
                    
                    // Increment and Record to ReplayManager
                    _replayManager.RecordCommand(command, state.MatchContext.ActivePlayer, ++_localSequenceCounter);
                }

                // Execute the command
                command.Execute(state);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error executing/recording command {command.GetType().Name}: {ex}", LogChannel.Error);
                throw; 
            }
        }
    }
}
