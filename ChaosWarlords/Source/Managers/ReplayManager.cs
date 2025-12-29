using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Data.Dtos;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Manages the recording and playback of game actions.
    /// </summary>
    public class ReplayManager : IReplayManager
    {
        private List<CommandDto> _recording = new List<CommandDto>();
        private bool _isReplaying;
        private readonly IGameLogger _logger;
        private int _sequenceCounter;

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
                var actions = System.Text.Json.JsonSerializer.Deserialize<List<CommandDto>>(replayJson);
                if (actions is not null)
                {
                    _isReplaying = true;
                    _recording.Clear();
                    _recording.AddRange(actions);
                    
                    // Initialize playback queue
                    _playbackQueue = new Queue<CommandDto>(actions);
                    
                    _logger.Log($"Replay loaded: {actions.Count} actions.", LogChannel.Info);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"Failed to load replay: {ex.Message}", LogChannel.Error);
            }
        }
        
        private Queue<CommandDto> _playbackQueue = new Queue<CommandDto>();

        public ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand? GetNextCommand(ChaosWarlords.Source.Core.Interfaces.State.IGameplayState state)
        {
            if (!_isReplaying) return null;
            
            if (_playbackQueue.Count == 0)
            {
                StopReplay();
                return null;
            }

            var dto = _playbackQueue.Dequeue();
            return DtoMapper.HydrateCommand(dto, state);
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

        public void RecordCommand(ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand command, ChaosWarlords.Source.Entities.Actors.Player actor)
        {
            if (_isReplaying) return;
            if (command == null) return;

            try
            {
                // Use the Mapper to create the DTO
                var dto = DtoMapper.ToDto(command, ++_sequenceCounter, actor);
                if (dto != null)
                {
                    _recording.Add(dto);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"Failed to record command: {ex.Message}", LogChannel.Error);
            }
        }
    }
}
