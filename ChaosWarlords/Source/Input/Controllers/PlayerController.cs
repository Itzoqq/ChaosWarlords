using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Input;
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
            // PRIORITY 1: Global Shortcuts (Escape) & Overlays
            if (HandleGlobalInput(e)) return;

            // PRIORITY 2: Popups & UI Interactions (Must handle before blocking checks!)
            if (HandlePopupInteractions(e)) return;

            // PRIORITY 3: Blocking Overlays (Blocks Map/Game input)
            if (IsInputBlocked()) return;

            // PRIORITY 4: Specific State Logic (Spy Selection)
            if (HandleSpySelectionInput(e)) return;
            if (HandleOpponentSelectionInput(e)) return;

            // Note: Coordinator logic is handled by Coordinator's own subscription to the same event.
            // We do not need to call _inputCoordinator.HandleEvent(e) because it listens independently.
            // However, IF blocking was required, the Coordinator should check blocking status or we should have a centralized handler.
            // currently, the Coordinator is a separate listener. 
            // Ideally, Coordinator should check "IsPaused" or similar state.
        }

        private bool IsInputBlocked()
        {
            // Market is handled by InputCoordinator, so we don't strictly block it, 
            // but Pause/Confirmation/OptionalPopup block everything.
            return _gameState.IsPauseMenuOpen ||
                   _gameState.IsConfirmationPopupOpen ||
                   _gameState.IsOptionalEffectPopupOpen;
        }

        private bool HandleGlobalInput(InputEventArgs e)
        {
            if (e.Type == InputEventType.KeyDown && e.Key == Keys.Escape)
            {
                _gameState.HandleEscapeKeyPress();
                return true;
            }
            if (e.Type == InputEventType.KeyDown && e.Key == Keys.Enter)
            {
                return HandleEnterKey();
            }
            if (e.Type == InputEventType.RightClick)
            {
                return HandleRightClick();
            }
            return false;
        }

        private bool HandleEnterKey()
        {
            if (IsInputBlocked() && !_gameState.IsConfirmationPopupOpen) return true;

            if (_gameState.IsConfirmationPopupOpen)
            {
                _gameState.UIManager.TriggerPopupConfirm();
                return true;
            }

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
            if (_gameState.IsMarketOpen)
            {
                _gameState.MarketStateManager.Close();
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
                _gameState.ActionSystem.FinalizeSpyReturn(clickedSpy.Value);
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

        private bool HandlePopupInteractions(InputEventArgs e)
        {
            // Optional Effect Popup Click
            // Now uses decoupled interface access
            if (_gameState.View != null && _gameState.IsOptionalEffectPopupOpen)
            {
                if (e.Type == InputEventType.LeftClick)
                {
                    var mousePos = e.Position.ToPoint();
                    _gameState.View.HandleOptionalEffectClick(mousePos.X, mousePos.Y);
                    
                    // Return true to block input if popup was visible
                    return true;
                }
            }
            return false;
        }
    }
}



