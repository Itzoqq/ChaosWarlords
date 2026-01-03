using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
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
        }

        /// <summary>
        /// Main update loop for player input handling.
        /// Returns true if input was handled and should block further processing.
        /// </summary>
        public bool Update()
        {
            if (HandleGlobalInput()) return true;
            if (HandleSpySelectionInput()) return true;

            // Handle optional effect popup clicks
            if (HandleOptionalEffectPopup()) return true;

            // CRITICAL: Block all game input when pause menu or popup is open
            // This prevents clicks from passing through UI to the game world
            if (_gameState.IsPauseMenuOpen || _gameState.IsConfirmationPopupOpen)
            {
                return true; // Input blocked
            }

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
                _gameState.Logger.Log(reason, LogChannel.Warning);
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
            if (site is null) return false;

            if (_interactionMapper is null) return false;

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

        private bool HandleOptionalEffectPopup()
        {
            // Check if optional effect popup is visible and handle clicks
            if (_gameState is GameStates.GameplayState gameplayState && 
                gameplayState._view is Rendering.Views.GameplayView view)
            {
                // If popup is visible, handle clicks and block other input
                if (view.HandViewModels != null) // Just checking if view is initialized
                {
                    if (_inputManager.IsLeftMouseJustClicked())
                    {
                        var mousePos = _inputManager.MousePosition.ToPoint();
                        view.HandleOptionalEffectClick(mousePos.X, mousePos.Y);
                        // Return true to block input if popup was visible
                        // The popup will handle its own visibility state
                    }
                }
            }
            return false;
        }
    }
}



