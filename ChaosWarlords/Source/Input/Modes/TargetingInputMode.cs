using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ChaosWarlords.Source.States.Input
{
    public class TargetingInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly InputManager _inputManager;
        private readonly IUISystem _uiManager;
        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public TargetingInputMode(IGameplayState state, InputManager inputManager, IUISystem uiManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // 1. SAFETY: State Desync Protection
            if (actionSystem.CurrentState == ActionState.Normal)
            {
                return new SwitchToNormalModeCommand();
            }

            // 2. UI Blocking
            if (_uiManager.IsMarketHovered || _uiManager.IsAssassinateHovered || _uiManager.IsReturnSpyHovered)
            {
                return null;
            }

            // 3. Right-Click to Cancel
            if (inputManager.IsRightMouseJustClicked())
            {
                // Safety Log
                string cardName = actionSystem.PendingCard != null ? actionSystem.PendingCard.Name : "Unknown";
                GameLogger.Log($"Input: Cancelled Action for {cardName}. Card returned to hand.", LogChannel.Info);

                actionSystem.CancelTargeting();
                // We return this command to ensure immediate update, 
                // though the event system could handle cancellation too if you wired OnActionCancelled.
                return new SwitchToNormalModeCommand();
            }

            // 4. Handle Specific Targeting Logic
            if (inputManager.IsLeftMouseJustClicked())
            {
                if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
                {
                    HandleSpySelection(inputManager, mapManager, activePlayer, actionSystem);
                    // Return null; if action completed, the event handler in GameplayState 
                    // will switch the mode for the next frame.
                    return null;
                }

                HandleTargetingClick(inputManager, mapManager, actionSystem);
                // Return null; if action completed, event handler handles state switch.
                return null;
            }

            return null;
        }

        private void HandleSpySelection(InputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            Site site = actionSystem.PendingSite;
            if (site == null)
            {
                actionSystem.CancelTargeting();
                return;
            }

            var enemies = mapManager.GetEnemySpiesAtSite(site, activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (inputManager.IsMouseOver(btnRect))
                {
                    // This will fire OnActionCompleted if successful
                    actionSystem.FinalizeSpyReturn(enemies[i]);
                    return;
                }
            }

            // If clicked outside buttons, maybe cancel?
            GameLogger.Log("Cancelled spy selection.", LogChannel.General);
            actionSystem.CancelTargeting();
        }

        private void HandleTargetingClick(InputManager inputManager, IMapManager mapManager, IActionSystem actionSystem)
        {
            Vector2 mousePos = inputManager.MousePosition;
            MapNode targetNode = mapManager.GetNodeAt(mousePos);
            Site targetSite = mapManager.GetSiteAt(mousePos);

            if (targetNode == null && targetSite == null)
            {
                return;
            }

            // This will fire OnActionCompleted if successful, or OnActionFailed if error
            actionSystem.HandleTargetClick(targetNode, targetSite);
        }
    }
}