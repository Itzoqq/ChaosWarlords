using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities; // For LogChannel, PlayerColor
using ChaosWarlords.Source.Contexts; // For MatchPhase

namespace ChaosWarlords.Source.Input.Controllers
{
    /// <summary>
    /// Manages the Replay lifecycle (Load, Save, Playback Loop).
    /// Decouples replay logic from the main GameplayState.
    /// </summary>
    public class ReplayController
    {
        private readonly IGameplayState _gameState;
        private readonly IReplayManager _replayManager;
        private readonly IInputManager _inputManager;
        private readonly IGameLogger _logger;
        private readonly Action _onReplayRestartRequested;

        // Playback State
        private float _replayTimer;
        private const float _replayDelay = 0.2f; // 200ms
        private bool _replayComplete;

        public ReplayController(
            IGameplayState gameState, 
            IReplayManager replayManager, 
            IInputManager inputManager, 
            IGameLogger logger,
            Action onReplayRestartRequested)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            _replayManager = replayManager ?? throw new ArgumentNullException(nameof(replayManager));
            _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _onReplayRestartRequested = onReplayRestartRequested;
        }

        public void Update(GameTime gameTime)
        {
            HandleSaveInput();
            HandleLoadInput();

            if (_replayManager.IsReplaying)
            {
                UpdatePlayback(gameTime);
            }
        }

        private void HandleSaveInput()
        {
            if (_inputManager.IsKeyJustPressed(Keys.F5))
            {
                // Accessing MatchContext through State interface (assumed exposed or passed)
                // GameplayState exposes MatchContext
                if (_gameState.MatchContext.CurrentPhase == MatchPhase.Setup)
                {
                    _logger.Log("Cannot save replay during setup phase! Complete initial deployment first.", LogChannel.Warning);
                }
                else if (!_replayManager.IsReplaying)
                {
                    string json = _replayManager.GetRecordingJson();
                    File.WriteAllText("last_replay.json", json);
                    _logger.Log("Replay saved to last_replay.json", LogChannel.Info);
                }
            }
        }

        private void HandleLoadInput()
        {
            if (_inputManager.IsKeyJustPressed(Keys.F6))
            {
                // Check for existing troop presence to prevent mid-game load
                bool anyTroopsPlaced = _gameState.MapManager.Nodes.Any(n => n.Occupant != PlayerColor.None && n.Occupant != PlayerColor.Neutral);
                
                if (anyTroopsPlaced)
                {
                    if (_replayManager.IsReplaying || _replayComplete)
                        _logger.Log("Cannot restart replay mid-game! Exit to main menu and start a new game to replay again.", LogChannel.Warning);
                    else
                        _logger.Log("Cannot start replay after troops have been placed! Start a new game first.", LogChannel.Warning);
                }
                else if (File.Exists("last_replay.json"))
                {
                    StartReplayFromFile("last_replay.json");
                }
                else
                {
                    _logger.Log("No replay file found. Play a game and press F5 to save a replay first.", LogChannel.Warning);
                }
            }
        }

        private void StartReplayFromFile(string path)
        {
            if (_replayManager.IsReplaying) _replayManager.StopReplay();

            _replayComplete = false;
            _replayTimer = 0f;

            string json = File.ReadAllText(path);
            _replayManager.StartReplay(json);
            
            // Callback to GameplayState to re-initialize match with new seed
            _onReplayRestartRequested?.Invoke();

            _logger.Log($"Replay started (Seed: {_replayManager.Seed}). Watch your previous game unfold!", LogChannel.Info);
        }

        private void UpdatePlayback(GameTime gameTime)
        {
            _replayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_replayTimer >= _replayDelay)
            {
                _replayTimer = 0f;

                var cmd = _replayManager.GetNextCommand(_gameState);
                if (cmd != null)
                {
                    // Execute command directly
                    cmd.Execute(_gameState);
                    _logger.Log($"Replay Executed: {cmd.GetType().Name} (ActivePlayer: {_gameState.TurnManager.ActivePlayer.Color})", LogChannel.Info);
                    
                    // Force view update provided by the state
                    // We can't call _view.Update() here easily unless exposed, 
                    // but GameplayState.Update calls View.Update at the end anyway.
                }
                else if (!_replayComplete)
                {
                    _replayComplete = true;
                    _logger.Log("=== REPLAY COMPLETE === Press F6 to restart", LogChannel.Info);
                }
            }
        }
    }
}
