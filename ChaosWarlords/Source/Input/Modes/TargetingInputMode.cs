using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ChaosWarlords.Source.States.Input
{
    public class TargetingInputMode : IInputMode
    {
        private readonly IGameplayState _state;
        private readonly IInputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMapManager _mapManager;
        private readonly ITurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public TargetingInputMode(IGameplayState state, IInputManager inputManager, IUIManager uiManager, IMapManager mapManager, ITurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // 1. SAFETY: State Desync Protection
            if (actionSystem.CurrentState == ActionState.Normal)
            {
                return new SwitchToNormalModeCommand();
            }

            // 2. UI Blocking
            if (IsUIBlocking())
            {
                return null;
            }

            // 3. Right-Click to Cancel
            if (inputManager.IsRightMouseJustClicked())
            {
                return HandleCancellation(actionSystem);
            }

            // 4. Handle Specific Targeting Logic
            if (inputManager.IsLeftMouseJustClicked())
            {
                return HandleLeftClickInternal(inputManager, mapManager, activePlayer, actionSystem);
            }

            return null;
        }

        private bool IsUIBlocking()
        {
            return _uiManager.IsMarketHovered || _uiManager.IsAssassinateHovered || _uiManager.IsReturnSpyHovered;
        }

        private SwitchToNormalModeCommand HandleCancellation(IActionSystem actionSystem)
        {
            // Safety Log
            string cardName = actionSystem.PendingCard is not null ? actionSystem.PendingCard.Name : "Unknown";
            _state.Logger.Log($"Input: Cancelled Action for {cardName}. Card returned to hand.", LogChannel.Info);

            actionSystem.CancelTargeting();
            // We return this command to ensure immediate update, 
            // though the event system could handle cancellation too if you wired OnActionCancelled.
            return new SwitchToNormalModeCommand();
        }

        private IGameCommand? HandleLeftClickInternal(IInputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                // Return the command from spy selection (ResolveSpyCommand usually)
                return HandleSpySelection(inputManager, mapManager, activePlayer, actionSystem);
            }

            Vector2 mousePos = inputManager.MousePosition;
            MapNode? targetNode = mapManager.GetNodeAt(mousePos);
            Site? targetSite = mapManager.GetSiteAt(mousePos);
            
            // Return the command if the click resolved an action
            return HandleTargetingClick(actionSystem, targetNode, targetSite);
        }

        private IGameCommand? HandleSpySelection(IInputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            Site? site = actionSystem.PendingSite;
            if (site is null)
            {
                actionSystem.CancelTargeting();
                return null;
            }

            var enemies = mapManager.GetEnemySpiesAtSite(site, activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (inputManager.IsMouseOver(btnRect))
                {
                    // This will return the ResolveSpyCommand
                    return actionSystem.FinalizeSpyReturn(enemies[i]);
                }
            }

            // If clicked outside buttons, maybe cancel?
            _state.Logger.Log("Cancelled spy selection.", LogChannel.General);
            actionSystem.CancelTargeting();
            return new SwitchToNormalModeCommand();
        }

        private static IGameCommand? HandleTargetingClick(IActionSystem actionSystem, MapNode? targetNode, Site? targetSite)
        {
            if (targetNode is null && targetSite is null)
            {
                return null;
            }

            // This will return the command (Assassinate, Deploy, etc) if valid
            return actionSystem.HandleTargetClick(targetNode, targetSite);
        }
    }
}




