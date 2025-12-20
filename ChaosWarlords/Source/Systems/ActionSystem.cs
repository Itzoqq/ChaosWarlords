using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System;
using System.Linq;

namespace ChaosWarlords.Source.Systems
{
    public class ActionSystem : IActionSystem
    {
        // Event Definitions
        public event EventHandler OnActionCompleted;
        public event EventHandler<string> OnActionFailed;

        public ActionState CurrentState { get; internal set; } = ActionState.Normal;
        public Card PendingCard { get; internal set; }
        public Site PendingSite { get; private set; }

        private Player _currentPlayer;
        private readonly IMapManager _mapManager; // Changed to Interface

        // Constructor now accepts IMapManager for NSubstitute compatibility
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
            const int cost = 3;
            if (_currentPlayer.Power < cost)
            {
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {cost}.");
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
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {cost}.");
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

        // Removed '?' from types to fix warnings
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

            if (PendingCard == null && _currentPlayer.Power < 3)
            {
                CancelTargeting();
                OnActionFailed?.Invoke(this, "Not enough Power to execute Assassinate!");
                return;
            }

            if (PendingCard == null) _currentPlayer.Power -= 3;

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

            if (PendingCard == null && _currentPlayer.Power < 3)
            {
                CancelTargeting();
                OnActionFailed?.Invoke(this, "Not enough Power to execute Return Spy!");
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
                bool success = _mapManager.ReturnSpecificSpy(targetSite, _currentPlayer, distinctEnemies[0]);
                if (success)
                {
                    if (PendingCard == null) _currentPlayer.Power -= 3;
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
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

            bool success = _mapManager.ReturnSpecificSpy(PendingSite, _currentPlayer, selectedSpyColor);
            if (success)
            {
                if (PendingCard == null) _currentPlayer.Power -= 3;
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}