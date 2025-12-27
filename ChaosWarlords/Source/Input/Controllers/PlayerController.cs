using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Interfaces;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using System;

namespace ChaosWarlords.Source.Input.Controllers
{
    /// <summary>
    /// Handles all local player input and translates it to game commands.
    /// Extracted from GameplayState to separate input handling from game state management.
    /// Industry precedent: Unity's PlayerInput, Unreal's PlayerController
    /// </summary>
    public class PlayerController
    {
        private readonly IGameplayState _gameState;
        private readonly IInputManager _inputManager;
        private readonly GameplayInputCoordinator _inputCoordinator;
        private readonly InteractionMapper _interactionMapper;

        public PlayerController(
            IGameplayState gameState,
            IInputManager inputManager,
            GameplayInputCoordinator inputCoordinator,
            InteractionMapper interactionMapper)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            _inputCoordinator = inputCoordinator ?? throw new ArgumentNullException(nameof(inputCoordinator));
            _interactionMapper = interactionMapper ?? throw new ArgumentNullException(nameof(interactionMapper));
        }

        /// <summary>
        /// Main update loop for player input handling.
        /// Returns true if input was handled and should block further processing.
        /// </summary>
        public bool Update()
        {
            if (HandleGlobalInput()) return true;
            if (HandleSpySelectionInput()) return true;

            // Delegate to input coordinator for mode-specific input
            _inputCoordinator.HandleInput();
            return false;
        }

        private bool HandleGlobalInput()
        {
            if (HandleEscapeKey()) return true;
            if (HandleEnterKey()) return true;
            if (HandleRightClick()) return true;
            return false;
        }

        private bool HandleEscapeKey()
        {
            if (!_inputManager.IsKeyJustPressed(Keys.Escape)) return false;

            // Delegate to UIEventMediator via game state
            // The game state will handle pause menu toggling
            _gameState.HandleEscapeKeyPress();
            return true;
        }

        private bool HandleEnterKey()
        {
            if (!_inputManager.IsKeyJustPressed(Keys.Enter)) return false;

            // Block if pause menu is open
            if (_gameState.IsPauseMenuOpen) return true;

            if (_gameState.CanEndTurn(out string reason))
            {
                _gameState.HandleEndTurnKeyPress();
            }
            else
            {
                GameLogger.Log(reason, LogChannel.Warning);
            }
            return true;
        }

        private bool HandleRightClick()
        {
            if (!_inputManager.IsRightMouseJustClicked()) return false;

            if (_gameState.IsMarketOpen)
            {
                _gameState.CloseMarket();
                return true;
            }

            if (_gameState.ActionSystem.IsTargeting())
            {
                _gameState.ActionSystem.CancelTargeting();
                _gameState.SwitchToNormalMode();
                return true;
            }

            return false;
        }

        private bool HandleSpySelectionInput()
        {
            if (_gameState.ActionSystem.CurrentState != ActionState.SelectingSpyToReturn)
                return false;

            if (!_inputManager.IsLeftMouseJustClicked()) return false;

            var site = _gameState.ActionSystem.PendingSite;
            if (site == null) return false;

            PlayerColor? clickedSpy = _interactionMapper.GetClickedSpyReturnButton(
                _inputManager.MousePosition.ToPoint(),
                site,
                _gameState.UIManager.ScreenWidth);

            if (clickedSpy.HasValue)
            {
                _gameState.ActionSystem.FinalizeSpyReturn(clickedSpy.Value);
            }

            return true;
        }
    }
}
