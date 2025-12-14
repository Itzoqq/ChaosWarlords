using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ChaosWarlords.Source.States.Input
{
    public class TargetingInputMode : IInputMode
    {
        private readonly GameplayState _state;
        private readonly InputManager _inputManager;
        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public TargetingInputMode(GameplayState state, InputManager inputManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // *** CRITICAL FIX: If the ActionSystem is done, but we are still here, switch back to normal mode. ***
            if (actionSystem.CurrentState == ActionState.Normal)
            {
                // This is the clean break from the targeting loop.
                // It switches the InputMode without logging another "Cancelled" message.
                return new SwitchToNormalModeCommand();
            }
            // ***************************************************************************************************

            // Delegate to specific targeting logic
            switch (actionSystem.CurrentState)
            {
                case ActionState.SelectingSpyToReturn:
                    return UpdateSpySelectionLogic(inputManager, mapManager, activePlayer, actionSystem);
                case ActionState.TargetingAssassinate:
                case ActionState.TargetingPlaceSpy:
                case ActionState.TargetingReturnSpy:
                case ActionState.TargetingReturn:
                case ActionState.TargetingSupplant:
                    return UpdateGeneralTargetingLogic(inputManager, mapManager, activePlayer, actionSystem);

                default:
                    // This is hit if the state is an unknown/invalid ActionState (not None).
                    // We force a cancel to clear the state and switch to normal mode.
                    return new CancelActionCommand();
            }
        }

        private IGameCommand UpdateGeneralTargetingLogic(InputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (!inputManager.IsLeftMouseJustClicked()) return null; // Return null if no click

            Vector2 mousePos = inputManager.MousePosition;
            MapNode targetNode = mapManager.GetNodeAt(mousePos);
            Site targetSite = mapManager.GetSiteAt(mousePos);

            bool success = actionSystem.HandleTargetClick(targetNode, targetSite);

            if (success)
            {
                // Action completed, resolve card and exit targeting mode
                // Instead of calling _state.OnActionCompleted(), we return a command
                return new ActionCompletedCommand();
            }

            return null; // Return null if no successful target was clicked
        }

        private IGameCommand UpdateSpySelectionLogic(InputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (!inputManager.IsLeftMouseJustClicked()) return null;

            Site site = actionSystem.PendingSite;
            if (site == null)
            {
                // Sanity check: If we're in selection mode but have no site, cancel.
                actionSystem.CancelTargeting();
                return new CancelActionCommand(); // Return a cancel command
            }

            // --- Simplified Spy Selection Click Logic ---
            var enemies = mapManager.GetEnemySpiesAtSite(site, activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (inputManager.IsMouseOver(btnRect))
                {
                    bool success = actionSystem.FinalizeSpyReturn(enemies[i]);
                    if (success)
                    {
                        // Action completed, resolve card and exit targeting mode
                        return new ActionCompletedCommand(); // Return the complete command
                    }
                    return null; // Handled the click, but no action was executed
                }
            }

            // Clicking empty space cancels the selection
            GameLogger.Log("Cancelled selection.", LogChannel.General);
            actionSystem.CancelTargeting();
            return new CancelActionCommand(); // Return a cancel command
        }
    }
}