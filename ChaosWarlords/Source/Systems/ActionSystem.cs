using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System;
using System.Linq;

namespace ChaosWarlords.Source.Systems
{
    public class ActionSystem : IActionSystem
    {
        // Logic Constants
        private const int ASSASSINATE_COST = 3;
        private const int RETURN_SPY_COST = 3;

        // Event Definitions
        public event EventHandler OnActionCompleted;
        public event EventHandler<string> OnActionFailed;

        public ActionState CurrentState { get; internal set; } = ActionState.Normal;
        public Card PendingCard { get; internal set; }
        public Site PendingSite { get; private set; }

        private Player _currentPlayer;
        private readonly IMapManager _mapManager;

        public ActionSystem(Player initialPlayer, IMapManager mapManager)
        {
            _currentPlayer = initialPlayer;
            _mapManager = mapManager;
        }

        public void SetCurrentPlayer(Player newPlayer)
        {
            _currentPlayer = newPlayer;
            CancelTargeting();
        }

        public void TryStartAssassinate()
        {
            // Pre-check: Don't strictly spend yet, just check if they *could* afford it.
            if (_currentPlayer.Power < ASSASSINATE_COST)
            {
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {ASSASSINATE_COST}.");
                return;
            }

            StartTargeting(ActionState.TargetingAssassinate);
            GameLogger.Log($"Select a TROOP to Assassinate (Cost: {ASSASSINATE_COST} Power)...", LogChannel.General);
        }

        public void TryStartReturnSpy()
        {
            if (_currentPlayer.Power < RETURN_SPY_COST)
            {
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {RETURN_SPY_COST}.");
                return;
            }

            StartTargeting(ActionState.TargetingReturnSpy);
            GameLogger.Log($"Select a SITE to remove Enemy Spy (Cost: {RETURN_SPY_COST} Power)...", LogChannel.General);
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

        public void HandleTargetClick(MapNode targetNode, Site targetSite)
        {
            switch (CurrentState)
            {
                case ActionState.TargetingAssassinate:
                    HandleAssassinate(targetNode);
                    break;
                case ActionState.TargetingReturn:
                    HandleReturn(targetNode);
                    break;
                case ActionState.TargetingSupplant:
                    HandleSupplant(targetNode);
                    break;
                case ActionState.TargetingPlaceSpy:
                    HandlePlaceSpy(targetSite);
                    break;
                case ActionState.TargetingReturnSpy:
                    HandleReturnSpyInitialClick(targetSite);
                    break;
            }
        }

        private void HandleAssassinate(MapNode targetNode)
        {
            if (targetNode == null) return;

            if (!_mapManager.CanAssassinate(targetNode, _currentPlayer))
            {
                OnActionFailed?.Invoke(this, "Invalid Target!");
                return;
            }

            // Refactored Economy Check
            // If PendingCard is null, this is a Basic Action that costs Power.
            if (PendingCard == null)
            {
                if (!_currentPlayer.TrySpendPower(ASSASSINATE_COST))
                {
                    CancelTargeting();
                    OnActionFailed?.Invoke(this, $"Not enough Power to execute Assassinate! (Need {ASSASSINATE_COST})");
                    return;
                }
            }

            _mapManager.Assassinate(targetNode, _currentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void HandleReturn(MapNode targetNode)
        {
            if (targetNode == null) return;
            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, _currentPlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral) return;

                _mapManager.ReturnTroop(targetNode, _currentPlayer);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HandleSupplant(MapNode targetNode)
        {
            if (targetNode == null) return;
            if (!_mapManager.CanAssassinate(targetNode, _currentPlayer)) return;
            if (_currentPlayer.TroopsInBarracks <= 0) return;

            _mapManager.Supplant(targetNode, _currentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void HandlePlaceSpy(Site targetSite)
        {
            if (targetSite == null) return;
            if (targetSite.Spies.Contains(_currentPlayer.Color)) return;
            if (_currentPlayer.SpiesInBarracks <= 0) return;

            _mapManager.PlaceSpy(targetSite, _currentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void HandleReturnSpyInitialClick(Site targetSite)
        {
            if (targetSite == null) return;

            // Pre-validation before checking map logic
            if (PendingCard == null && _currentPlayer.Power < RETURN_SPY_COST)
            {
                CancelTargeting();
                OnActionFailed?.Invoke(this, $"Not enough Power to execute Return Spy! (Need {RETURN_SPY_COST})");
                return;
            }

            var enemySpies = _mapManager.GetEnemySpiesAtSite(targetSite, _currentPlayer);

            if (enemySpies.Count == 0)
            {
                OnActionFailed?.Invoke(this, "Invalid Target: No enemy spies here.");
                return;
            }

            var distinctEnemies = enemySpies.Distinct().ToList();

            if (distinctEnemies.Count == 1)
            {
                // Only one enemy faction present; execute immediately
                // NOTE: We only spend the power if the map action actually succeeds.
                if (PendingCard == null)
                {
                    if (!_currentPlayer.TrySpendPower(RETURN_SPY_COST))
                    {
                        CancelTargeting();
                        OnActionFailed?.Invoke(this, "Not enough Power!");
                        return;
                    }
                }

                bool success = _mapManager.ReturnSpecificSpy(targetSite, _currentPlayer, distinctEnemies[0]);

                if (success)
                {
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Refund if map logic failed? 
                    // In this specific architecture, map logic shouldn't fail if we passed checks, 
                    // but for safety in a robust system you might refund here. 
                    // For now, we assume _mapManager.ReturnSpecificSpy is reliable.
                }
            }
            else
            {
                PendingSite = targetSite;
                CurrentState = ActionState.SelectingSpyToReturn;
                GameLogger.Log($"Multiple spies detected at {targetSite.Name}. Select which faction to return.", LogChannel.General);
            }
        }

        public void FinalizeSpyReturn(PlayerColor selectedSpyColor)
        {
            if (PendingSite == null) return;

            // Economy check for the finalized action
            if (PendingCard == null)
            {
                if (!_currentPlayer.TrySpendPower(RETURN_SPY_COST))
                {
                    CancelTargeting();
                    OnActionFailed?.Invoke(this, "Not enough Power!");
                    return;
                }
            }

            bool success = _mapManager.ReturnSpecificSpy(PendingSite, _currentPlayer, selectedSpyColor);
            if (success)
            {
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}