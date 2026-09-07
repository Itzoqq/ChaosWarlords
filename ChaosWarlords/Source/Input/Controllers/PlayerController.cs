using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Entities.Cards;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Source.Input.Controllers
{
    /// <summary>
    /// Handles all local player input and translates it to game commands.
    /// Event-Driven Refactor: Jan 2026
    /// </summary>
    public class PlayerController
    {
        private readonly IGameplayState _gameState;
        private readonly IInputManager _inputManager;
        private readonly IGameplayInputCoordinator _inputCoordinator;
        private readonly IInteractionMapper? _interactionMapper;

        public PlayerController(
            IGameplayState gameState,
            IInputManager inputManager,
            IGameplayInputCoordinator inputCoordinator,
            IInteractionMapper? interactionMapper)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            _inputCoordinator = inputCoordinator ?? throw new ArgumentNullException(nameof(inputCoordinator));
            _interactionMapper = interactionMapper;

            _inputManager.OnInputEvent += HandleInputEvent;
        }

        public void Update()
        {
            // Coordinator now handles updates itself via event subscription too, 
            // OR if it has continuous update logic (HandleUpdate), we call it here.
            _inputCoordinator.HandleInput(); // This calls HandleUpdate on current mode
        }

        private void HandleInputEvent(object? sender, InputEventArgs e)
        {
            // Popups/pause-menu/Escape/Enter/gameplay-cancel input is ALL handled exclusively
            // by GameplayInputCoordinator now, in a single ordered pipeline - a second,
            // independent subscriber (this class) reacting to the SAME raw event used to also
            // handle a subset of it directly, which looked "mutually exclusive" from the
            // blocked/unblocked split alone but wasn't in practice: one handler's own side
            // effect (e.g. opening the pause menu) could flip the very flag the OTHER handler
            // was about to re-check for the SAME event, causing genuine double-processing (see
            // planning.txt for the concrete repro this was verified against). This class now
            // owns only the 2 narrow, still-independent responsibilities below, which don't
            // mutate anything GameplayInputCoordinator's own dispatch reads.
            if (IsInputBlocked()) return;

            if (HandleSpySelectionInput(e)) return;
            if (HandleOpponentSelectionInput(e)) return;
        }

        private bool IsInputBlocked()
        {
            return _gameState.IsPauseMenuOpen ||
                   _gameState.IsConfirmationPopupOpen ||
                   _gameState.IsOptionalEffectPopupOpen;
        }

        private bool HandleSpySelectionInput(InputEventArgs e)
        {
            if (_gameState.ActionSystem.CurrentState != ActionState.SelectingSpyToReturn)
                return false;

            if (e.Type != InputEventType.LeftClick) return false;

            var site = _gameState.ActionSystem.PendingSite;
            if (site is null) return false;
            if (_interactionMapper is null) return false;

            PlayerColor? clickedSpy = _interactionMapper.GetClickedSpyReturnButton(
                e.Position.ToPoint(),
                site,
                _gameState.UIManager.ScreenWidth);

            if (clickedSpy.HasValue)
            {
                // FinalizeSpyReturn only constructs the ResolveSpyCommand - it doesn't mutate
                // state or dispatch it itself (same split as every other "resolve a pending
                // targeting click" path in this codebase), so it must go through
                // RecordAndExecuteCommand like any other player-initiated command, for the same
                // replay/rollback guarantees.
                IGameCommand? command = _gameState.ActionSystem.FinalizeSpyReturn(clickedSpy.Value);
                if (command != null)
                {
                    _gameState.RecordAndExecuteCommand(command);
                }
                return true;
            }
            return false;
        }

        private bool HandleOpponentSelectionInput(InputEventArgs e)
        {
            if (_gameState.ActionSystem.CurrentState != ActionState.TargetingOpponentSelect)
                return false;

            if (e.Type != InputEventType.LeftClick) return false;
            if (_interactionMapper is null) return false;

            var allPlayers = _gameState.MatchContext.TurnManager.Players;
            var activePlayer = _gameState.MatchContext.TurnManager.ActivePlayer;
            int eligibilityThreshold = GetSelectOpponentThreshold(_gameState.ActionSystem.PendingCard);

            PlayerColor? clickedColor = _interactionMapper.GetClickedOpponentSelectButton(
                e.Position.ToPoint(),
                allPlayers,
                activePlayer,
                eligibilityThreshold,
                _gameState.UIManager.ScreenWidth);

            if (clickedColor.HasValue)
            {
                _gameState.RecordAndExecuteCommand(new SelectOpponentCommand(clickedColor.Value));
                return true;
            }
            return false;
        }

        private static int GetSelectOpponentThreshold(Card? sourceCard)
        {
            if (sourceCard == null) return 0;
            var effect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.SelectOpponent);
            return effect?.Amount ?? 0;
        }

    }
}



