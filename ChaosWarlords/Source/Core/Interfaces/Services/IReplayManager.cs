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
    }
}
