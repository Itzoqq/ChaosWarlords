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

        // FIX: Changed from UIManager to IUISystem to match constructor injection
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
            // Note: These properties must exist on IUISystem interface
            if (_uiManager.IsMarketHovered || _uiManager.IsAssassinateHovered || _uiManager.IsReturnSpyHovered)
            {
                return null;
            }

            // 3. Right-Click to Cancel
            if (inputManager.IsRightMouseJustClicked())
            {
                actionSystem.CancelTargeting();
                return new SwitchToNormalModeCommand();
            }

            // 4. Handle Specific Targeting Logic
            if (inputManager.IsLeftMouseJustClicked())
            {
                if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
                {
                    return HandleSpySelection(inputManager, mapManager, activePlayer, actionSystem);
                }

                IGameCommand cmd = HandleTargetingClick(inputManager, mapManager, actionSystem);
                if (cmd != null) return cmd;
            }

            return null;
        }

        private IGameCommand HandleSpySelection(InputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            Site site = actionSystem.PendingSite;
            if (site == null)
            {
                actionSystem.CancelTargeting();
                return new CancelActionCommand();
            }

            var enemies = mapManager.GetEnemySpiesAtSite(site, activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (inputManager.IsMouseOver(btnRect))
                {
                    bool success = actionSystem.FinalizeSpyReturn(enemies[i]);
                    if (success) return new ActionCompletedCommand();
                }
            }

            GameLogger.Log("Cancelled spy selection.", LogChannel.General);
            actionSystem.CancelTargeting();
            return new SwitchToNormalModeCommand();
        }

        private IGameCommand HandleTargetingClick(InputManager inputManager, IMapManager mapManager, IActionSystem actionSystem)
        {
            Vector2 mousePos = inputManager.MousePosition;
            MapNode targetNode = mapManager.GetNodeAt(mousePos);
            Site targetSite = mapManager.GetSiteAt(mousePos);

            if (targetNode == null && targetSite == null)
            {
                return null;
            }

            bool success = actionSystem.HandleTargetClick(targetNode, targetSite);

            if (success)
            {
                return new ActionCompletedCommand();
            }

            return null;
        }
    }
}