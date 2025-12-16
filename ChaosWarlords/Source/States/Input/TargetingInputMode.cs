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
        // FIX 1: Change private field type to the interface
        private readonly IGameplayState _state;

        // These fields are kept because they are directly used in the logic below, 
        // making the methods cleaner than accessing everything via _state.
        private readonly InputManager _inputManager;
        private readonly IMapManager _mapManager;
        private readonly TurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        // FIX 2: Change constructor parameter type to the interface
        public TargetingInputMode(IGameplayState state, InputManager inputManager, IMapManager mapManager, TurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // CRITICAL FIX: If the ActionSystem's pending state is reset, switch back to normal mode.
            if (actionSystem.CurrentState == ActionState.Normal)
            {
                // This is the clean break from the targeting loop.
                return new SwitchToNormalModeCommand();
            }

            // 1. Handle Spy Selection Logic (used after clicking a site for TargetReturnSpy)
            if (actionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                if (inputManager.IsLeftMouseJustClicked())
                {
                    return HandleSpySelection(inputManager, mapManager, activePlayer, actionSystem);
                }
                return null;
            }

            // 2. Handle Map Targeting Logic (used for Deploy, Assassinate, Supplant, PlaceSpy)
            if (inputManager.IsLeftMouseJustClicked())
            {
                return HandleTargetingClick(inputManager, mapManager, actionSystem);
            }

            return null;
        }

        private IGameCommand HandleTargetingClick(InputManager inputManager, IMapManager mapManager, IActionSystem actionSystem)
        {
            Vector2 mousePos = inputManager.MousePosition;
            MapNode targetNode = mapManager.GetNodeAt(mousePos);
            Site targetSite = mapManager.GetSiteAt(mousePos);

            bool success = actionSystem.HandleTargetClick(targetNode, targetSite);

            if (success)
            {
                // Action completed, resolve card and exit targeting mode
                return new ActionCompletedCommand();
            }

            // Clicking invalid space does nothing, allowing the user to try again
            return null;
        }

        private IGameCommand HandleSpySelection(InputManager inputManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            Site site = actionSystem.PendingSite;
            if (site == null)
            {
                // Sanity check: If we're in selection mode but have no site, cancel.
                actionSystem.CancelTargeting();
                return new CancelActionCommand();
            }

            // --- Simplified Spy Selection Click Logic ---
            var enemies = mapManager.GetEnemySpiesAtSite(site, activePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (inputManager.IsMouseOver(btnRect))
                {
                    // Return the command that will finalize the action and switch back to normal mode
                    return new ResolveSpyCommand(enemies[i]);
                }
            }

            // Clicking empty space cancels the selection
            GameLogger.Log("Cancelled selection.", LogChannel.General);
            actionSystem.CancelTargeting();
            return new CancelActionCommand(); // Return a cancel command
        }
    }
}