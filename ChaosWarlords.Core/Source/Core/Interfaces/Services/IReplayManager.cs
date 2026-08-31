namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Handles the recording and playback of gameplay sessions.
    /// </summary>
    public interface IReplayManager
    {
        /// <summary>
        /// Gets a value indicating whether a replay is currently actively playing back.
        /// </summary>
        bool IsReplaying { get; }

        /// <summary>
        /// Initializes and starts a replay session from the provided JSON recording data.
        /// </summary>
        /// <param name="replayJson">The serialized recording data.</param>
        void StartReplay(string replayJson);

        /// <summary>
        /// Retrieves the current recording of the active session as a JSON string.
        /// </summary>
        /// <returns>A JSON string representing the sequence of recorded actions.</returns>
        string GetRecordingJson();

        /// <summary>
        /// Stops the current replay and returns control to the normal game flow or menu.
        /// </summary>
        void StopReplay();

        void RecordCommand(Logic.IGameCommand command, Entities.Actors.Player actor, int sequenceNumber);

        /// <summary>
        /// Number of commands recorded so far in the current session. Combined with
        /// InsertCommand, lets a caller "reserve" the position a command will occupy in the
        /// recording BEFORE executing it, so a command whose Execute() synchronously
        /// triggers a nested dispatch (e.g. CommandDispatcher.Dispatch recursing) still ends
        /// up recorded BEFORE whatever it triggered, not after. See CommandDispatcher.Dispatch
        /// and planning.txt for the ordering bug this exists to prevent.
        /// </summary>
        int RecordingCount { get; }

        /// <summary>
        /// Records a command at a specific position in the recording, rather than appending
        /// (see RecordingCount's doc comment for why). RecordCommand is equivalent to
        /// InsertCommand(RecordingCount, ...) - a plain append.
        /// </summary>
        void InsertCommand(int index, Logic.IGameCommand command, Entities.Actors.Player actor, int sequenceNumber);

        /// <summary>
        /// Gets the seed used for the recorded session.
        /// Only valid after StartReplay or during recording.
        /// </summary>
        int Seed { get; }

        /// <summary>
        /// Initializes a new recording session with the specified seed.
        /// </summary>
        void InitializeRecording(int seed);

        /// <summary>
        /// Retrieves the next command from the replay queue, if available.
        /// </summary>
        Logic.IGameCommand? GetNextCommand(Source.Contexts.MatchContext context);
    }
}
