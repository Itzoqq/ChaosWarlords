using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Utilities; // For LogChannel, PlayerColor
using ChaosWarlords.Source.Contexts; // For MatchPhase
using ChaosWarlords.Source.Core.Events;
using System;
using System.IO;
using System.Linq;

namespace ChaosWarlords.Source.Input.Controllers
{
    /// <summary>
    /// Manages the Replay lifecycle (Load, Save, Playback Loop).
    /// Decouples replay logic from the main GameplayState.
    /// Event-Driven Refactor: Jan 2026
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

            // Subscribe to Input Events
            _inputManager.OnInputEvent += HandleInputEvent;
        }

        public void Update(GameTime gameTime)
        {
            // Input is now handled via events

            if (_replayManager.IsReplaying)
            {
                UpdatePlayback(gameTime);
            }
        }

        private void HandleInputEvent(object? sender, InputEventArgs e)
        {
            if (e.Type != InputEventType.KeyDown) return;

            if (e.Key == Keys.F5)
            {
                HandleSaveReplay();
            }
            else if (e.Key == Keys.F6)
            {
                HandleLoadReplay();
            }
        }

        private void HandleSaveReplay()
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

        private void HandleLoadReplay()
        {
            // Check for existing troop presence to prevent mid-game load
            bool anyTroopsPlaced = _gameState.MatchContext.MapManager.Nodes.Any(n => n.Occupant != PlayerColor.None && n.Occupant != PlayerColor.Neutral);

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

                var cmd = _replayManager.GetNextCommand(_gameState.MatchContext);
                if (cmd != null)
                {
                    // Execute command directly (bypassing CommandDispatcher.Dispatch - we
                    // trust the recorded stream and don't want to re-Validate or re-record
                    // during replay). But Dispatch normally increments MatchContext.
                    // SequenceNumber for every command BEFORE executing it, and nothing else
                    // in the replay path does that - so without this line, SequenceNumber
                    // would sit frozen at whatever it was when replay started, for the
                    // entire replay, no matter how many commands run. That's a real replay-
                    // fidelity bug (found via ReplayFidelityTests.cs): GetStateHash() folds
                    // SequenceNumber in, so a replayed game's hash would never match the live
                    // game that produced the recording, and any future networking code that
                    // uses SequenceNumber for ordering/reconciliation would see it stuck.
                    // Mirror Dispatch's own ordering (increment before Execute) so a command
                    // whose Execute() raises an event another command reacts to synchronously
                    // still sees a consistent count. See planning.txt.
                    _gameState.MatchContext.SequenceNumber++;
                    cmd.Execute(_gameState.MatchContext);
                    _logger.Log($"Replay Executed: {cmd.GetType().Name} (ActivePlayer: {_gameState.MatchContext.TurnManager.ActivePlayer.Color})", LogChannel.Info);

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
