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
        private List<GameCommandDto> _recording = new List<GameCommandDto>();
        private bool _isReplaying;
        private readonly IGameLogger _logger;
        private int _seed;

        public ReplayManager(IGameLogger logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public bool IsReplaying => _isReplaying;
        public int Seed => _seed;

        public void InitializeRecording(int seed)
        {
            _seed = seed;
            _recording.Clear();
            _logger.Log($"Replay recording initialized with seed: {seed}", LogChannel.Info);
        }

        public void StartReplay(string replayJson)
        {
            _logger.Log("Starting Replay...", LogChannel.Info);
            try 
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<ReplayDataDto>(replayJson);
                if (data is not null)
                {
                    _isReplaying = true;
                    _seed = data.Seed;
                    _recording.Clear();
                    _recording.AddRange(data.Commands);
                    
                    // Initialize playback queue
                    _playbackQueue = new Queue<GameCommandDto>(data.Commands);
                    
                    _logger.Log($"Replay loaded: {data.Commands.Count} actions (Seed: {_seed}).", LogChannel.Info);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"Failed to load replay: {ex.Message}", LogChannel.Error);
            }
        }
        
        private Queue<GameCommandDto> _playbackQueue = new Queue<GameCommandDto>();

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
            var data = new ReplayDataDto
            {
                Seed = _seed,
                Commands = _recording
            };
            return System.Text.Json.JsonSerializer.Serialize(data);
        }

        public void StopReplay()
        {
            _isReplaying = false;
            _logger.Log("Replay Stopped.", LogChannel.Info);
        }

        public void RecordCommand(ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand command, ChaosWarlords.Source.Entities.Actors.Player actor, int sequenceNumber)
        {
            if (_isReplaying) return;
            if (command == null) return;

            try
            {
                // Use the Mapper to create the DTO
                var dto = DtoMapper.ToDto(command, sequenceNumber, actor);
                if (dto != null)
                {
                    _recording.Add(dto);
                    _logger.Log($"[ReplayManager] Recorded {dto.GetType().Name} (Seq: {dto.Seq})", LogChannel.Info);
                }
                else
                {
                    _logger.Log($"[ReplayManager] DtoMapper returned null for {command.GetType().Name}.", LogChannel.Warning);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"Failed to record command: {ex.Message}", LogChannel.Error);
            }
        }
    }
}
