using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    public class CommandDispatcher : ICommandDispatcher
    {
        private readonly IReplayManager _replayManager;
        private readonly IGameLogger _logger;


        public CommandDispatcher(IReplayManager replayManager, IGameLogger logger)
        {
            _replayManager = replayManager ?? throw new ArgumentNullException(nameof(replayManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Dispatch(IGameCommand command, Contexts.MatchContext context)
        {
            try
            {
                // Validate (Strict Server-Side Validation)
                if (!command.Validate(context))
                {
                    _logger.Log($"Validation failed for command {command.GetType().Name}", LogChannel.Warning);
                    return;
                }

                // Increment Sequence Number (Authority)
                context.SequenceNumber++;

                // Record the command for replay (unless we're currently replaying)
                if (!_replayManager.IsReplaying)
                {
                    context.RecordAction(command.GetType().Name, command.ToString() ?? "Command");

                    // Record to ReplayManager using the authoritative sequence number
                    _replayManager.RecordCommand(command, context.ActivePlayer, (int)context.SequenceNumber);
                }

                // Execute the command via MatchContext (Transaction)
                command.Execute(context);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error executing/recording command {command.GetType().Name}: {ex}", LogChannel.Error);
                throw;
            }
        }
    }
}
