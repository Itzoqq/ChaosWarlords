using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Source.Systems
{
    public class ActionSystem
    {
        public ActionState CurrentState { get; internal set; } = ActionState.Normal;
        public Card PendingCard { get; internal set; }
        public Site PendingSite { get; private set; }

        private Player _currentPlayer;
        private readonly MapManager _mapManager;

        public ActionSystem(Player initialPlayer, MapManager mapManager)
        {
            _currentPlayer = initialPlayer;
            _mapManager = mapManager;
        }

        /// <summary>
        /// Updates the internal player reference when the turn changes.
        /// </summary>
        public void SetCurrentPlayer(Player newPlayer)
        {
            _currentPlayer = newPlayer;
            CancelTargeting(); // Always cancel any pending action when switching players
        }

        public void TryStartAssassinate()
        {
            const int cost = 3;
            if (_currentPlayer.Power < cost)
            {
                GameLogger.Log($"Not enough Power! Need {cost}.", LogChannel.Economy);
                return;
            }

            StartTargeting(ActionState.TargetingAssassinate);
            GameLogger.Log($"Select a TROOP to Assassinate (Cost: {cost} Power)...", LogChannel.General);
        }

        public void TryStartReturnSpy()
        {
            const int cost = 3;
            if (_currentPlayer.Power < cost)
            {
                GameLogger.Log($"Not enough Power! Need {cost}.", LogChannel.Economy);
                return;
            }

            StartTargeting(ActionState.TargetingReturnSpy);
            GameLogger.Log($"Select a SITE to remove Enemy Spy (Cost: {cost} Power)...", LogChannel.General);
        }

        public void StartTargeting(ActionState state, Card card = null)
        {
            CurrentState = state;
            PendingCard = card;
        }

        public void CancelTargeting()
        {
            CurrentState = ActionState.Normal;
            PendingCard = null;
            GameLogger.Log("Targeting Cancelled.", LogChannel.General);
        }

        public bool IsTargeting()
        {
            return CurrentState != ActionState.Normal;
        }

        /// <summary>
        /// Attempts to resolve a targeting action based on the current state and mouse click.
        /// </summary>
        /// <returns>True if the action was successfully completed, allowing Game1 to finalize costs/card effects.</returns>
        public bool HandleTargetClick(MapNode targetNode, Site targetSite)
        {
            switch (CurrentState)
            {
                case ActionState.TargetingAssassinate:
                    return HandleAssassinate(targetNode);
                case ActionState.TargetingReturn:
                    return HandleReturn(targetNode);
                case ActionState.TargetingSupplant:
                    return HandleSupplant(targetNode);
                case ActionState.TargetingPlaceSpy:
                    return HandlePlaceSpy(targetSite);
                case ActionState.TargetingReturnSpy:
                    return HandleReturnSpyInitialClick(targetSite);
                default:
                    return false;
            }
        }

        private bool HandleAssassinate(MapNode targetNode)
        {
            if (targetNode == null) return false;

            // 1. Verify Logic (Map rules)
            if (!_mapManager.CanAssassinate(targetNode, _currentPlayer))
            {
                GameLogger.Log("Invalid Target!", LogChannel.Error);
                return false;
            }

            // 2. Verify Cost (Edge Case Protection)
            // Only check cost if this action isn't free (provided by a Card)
            if (PendingCard == null && _currentPlayer.Power < 3)
            {
                GameLogger.Log("Not enough Power to execute Assassinate!", LogChannel.Economy);
                CancelTargeting(); // Auto-cancel if they can't afford it anymore
                return false;
            }

            // 3. Execute
            if (PendingCard == null) _currentPlayer.Power -= 3;

            _mapManager.Assassinate(targetNode, _currentPlayer);
            return true;
        }

        private bool HandleReturn(MapNode targetNode)
        {
            if (targetNode == null) return false;
            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, _currentPlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral) return false;
                _mapManager.ReturnTroop(targetNode, _currentPlayer);
                return true;
            }
            return false;
        }

        private bool HandleSupplant(MapNode targetNode)
        {
            if (targetNode == null) return false;
            if (!_mapManager.CanAssassinate(targetNode, _currentPlayer)) return false;
            if (_currentPlayer.TroopsInBarracks <= 0) return false;
            _mapManager.Supplant(targetNode, _currentPlayer);
            return true;
        }

        private bool HandlePlaceSpy(Site targetSite)
        {
            if (targetSite == null) return false;
            if (targetSite.Spies.Contains(_currentPlayer.Color)) return false;
            if (_currentPlayer.SpiesInBarracks <= 0) return false;
            _mapManager.PlaceSpy(targetSite, _currentPlayer);
            return true;
        }

        private bool HandleReturnSpyInitialClick(Site targetSite)
        {
            if (targetSite == null) return false;

            // --- FIX START: Verify Cost (Edge Case Protection) ---
            // Just like Assassinate, we must check if they can still afford it 
            // (e.g., if they lost power between clicking the button and clicking the map)
            if (PendingCard == null && _currentPlayer.Power < 3)
            {
                GameLogger.Log("Not enough Power to execute Return Spy!", LogChannel.Economy);
                CancelTargeting(); // Auto-cancel if they can't afford it anymore
                return false;
            }
            // --- FIX END ---

            // 1. Get all potential targets
            var enemySpies = _mapManager.GetEnemySpiesAtSite(targetSite, _currentPlayer);

            if (enemySpies.Count == 0)
            {
                GameLogger.Log("Invalid Target: No enemy spies here.", LogChannel.Error);
                return false;
            }

            // 2. Check how many DISTINCT enemy players are there
            var distinctEnemies = enemySpies.Distinct().ToList();

            if (distinctEnemies.Count == 1)
            {
                // No choice needed, execute immediately
                bool success = _mapManager.ReturnSpecificSpy(targetSite, _currentPlayer, distinctEnemies[0]);
                if (success)
                {
                    if (PendingCard == null) _currentPlayer.Power -= 3;
                }
                return success;
            }
            else
            {
                // Ambiguity! Multiple enemy factions present.
                // Transition to Selection State
                PendingSite = targetSite;
                CurrentState = ActionState.SelectingSpyToReturn;
                GameLogger.Log($"Multiple spies detected at {targetSite.Name}. Select which faction to return.", LogChannel.General);

                // Return false because the action is NOT complete yet. 
                // We are waiting for the UI selection.
                return false;
            }
        }

        /// <summary>
        /// Called by the UI when the player clicks a specific Spy Color button in the popup
        /// </summary>
        public bool FinalizeSpyReturn(PlayerColor selectedSpyColor)
        {
            if (PendingSite == null) return false;

            bool success = _mapManager.ReturnSpecificSpy(PendingSite, _currentPlayer, selectedSpyColor);
            if (success)
            {
                if (PendingCard == null) _currentPlayer.Power -= 3;
            }
            return success;
        }
    }
}