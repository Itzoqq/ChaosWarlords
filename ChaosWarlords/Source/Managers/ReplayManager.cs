using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Data.Recording;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Manages the recording and playback of game actions.
    /// </summary>
    public class ReplayManager : IReplayManager
    {
        private List<ReplayAction> _recording = new List<ReplayAction>();
        private bool _isReplaying;
        private readonly IGameLogger _logger;

        public ReplayManager(IGameLogger logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public bool IsReplaying => _isReplaying;

        public void StartReplay(string replayJson)
        {
            _logger.Log("Starting Replay...", LogChannel.Info);
            try 
            {
                var actions = System.Text.Json.JsonSerializer.Deserialize<List<ReplayAction>>(replayJson);
                if (actions is not null)
                {
                    _isReplaying = true;
                    _recording.Clear();
                    _recording.AddRange(actions);
                    _logger.Log($"Replay loaded: {actions.Count} actions.", LogChannel.Info);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"Failed to load replay: {ex.Message}", LogChannel.Error);
            }
        }

        public string GetRecordingJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(_recording);
        }

        public void StopReplay()
        {
            _isReplaying = false;
            _logger.Log("Replay Stopped.", LogChannel.Info);
        }

        public void RecordAction(ReplayAction action)
        {
            if (!_isReplaying && action is not null)
            {
                _recording.Add(action);
            }
        }
    }
}
