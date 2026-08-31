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

                // Increment Sequence Number (Authority) and capture THIS command's own
                // number and actor, and reserve its position in the recording, all BEFORE
                // Execute() runs - not after. This matters whenever Execute() synchronously
                // triggers a NESTED Dispatch() call (e.g. MapManager.OnSetupDeploymentComplete
                // auto-issuing an EndTurnCommand from inside a DeployTroopCommand's own
                // Execute()): reading SequenceNumber/ActivePlayer or appending to the
                // recording AFTER Execute() returns picks up whatever the NESTED command
                // already changed them to, not this command's own values. Confirmed this was
                // a real, not just theoretical, bug via ReplayFidelityTests.cs: the recorded
                // JSON for exactly this scenario showed the nested EndTurnCommand recorded
                // BEFORE the DeployTroopCommand that triggered it, both sharing the SAME
                // (wrong, LATER) sequence number, and both attributed to the WRONG actor
                // (whoever ActivePlayer became AFTER EndTurn switched it, not whoever
                // actually issued either command) - replay would then execute EndTurn before
                // the map even had the deploying player's troop on it. See planning.txt.
                context.SequenceNumber++;
                int mySequenceNumber = (int)context.SequenceNumber;
                var actor = context.ActivePlayer;
                int recordingSlot = _replayManager.IsReplaying ? -1 : _replayManager.RecordingCount;

                // Execute the command via MatchContext (Transaction)
                command.Execute(context);

                // Record the command for replay (unless we're currently replaying). Inserted
                // at the slot reserved above (not appended) so a nested Dispatch() triggered
                // during Execute() - which reserves ITS OWN slot at the same snapshot and
                // therefore inserts before this command finishes recording - ends up placed
                // AFTER this command once this insert shifts it, not before.
                if (!_replayManager.IsReplaying)
                {
                    context.RecordAction(command.GetType().Name, command.ToString() ?? "Command");
                    _replayManager.InsertCommand(recordingSlot, command, actor, mySequenceNumber);
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
