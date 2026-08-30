using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Utilities;
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
            // Snapshot before execution so a failure partway through can be rolled back,
            // leaving MatchContext consistent instead of partially mutated. Best-effort:
            // if the context can't be fully snapshotted (e.g. a partial test double), we
            // simply proceed without rollback capability rather than failing the dispatch.
            var snapshot = TryCreateSnapshot(context);

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

                // Execute the command via MatchContext (Transaction)
                command.Execute(context);

                // Record the command for replay (unless we're currently replaying)
                if (!_replayManager.IsReplaying)
                {
                    context.RecordAction(command.GetType().Name, command.ToString() ?? "Command");

                    // Record to ReplayManager using the authoritative sequence number
                    _replayManager.RecordCommand(command, context.ActivePlayer, (int)context.SequenceNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error executing/recording command {command.GetType().Name}: {ex}", LogChannel.Error);

                if (snapshot != null)
                {
                    _logger.Log($"Rolling back MatchContext to pre-command snapshot (Seq {snapshot.SequenceNumber}).", LogChannel.Warning);
                    StateRestorer.RestoreState(context, snapshot);
                }

                throw;
            }
        }

        private GameStateDto? TryCreateSnapshot(Contexts.MatchContext context)
        {
            try
            {
                return DtoMapper.ToGameStateDto(context);
            }
            catch (Exception ex)
            {
                _logger.Log($"CommandDispatcher: Could not snapshot state for rollback ({ex.Message}). Proceeding without rollback capability.", LogChannel.Warning);
                return null;
            }
        }
    }
}
