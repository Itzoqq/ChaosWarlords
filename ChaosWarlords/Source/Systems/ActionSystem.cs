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

        private readonly ITurnManager _turnManager;
        private readonly IMapManager _mapManager;

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public MapNode PendingMoveSource { get; private set; }

        public ActionSystem(ITurnManager turnManager, IMapManager mapManager)
        {
            _turnManager = turnManager;
            _mapManager = mapManager;
        }

        public void TryStartAssassinate()
        {
            if (CurrentPlayer.Power < ASSASSINATE_COST)
            {
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {ASSASSINATE_COST}.");
                return;
            }

            StartTargeting(ActionState.TargetingAssassinate);
            GameLogger.Log($"Select a TROOP to Assassinate (Cost: {ASSASSINATE_COST} Power)...", LogChannel.General);
        }

        public void TryStartReturnSpy()
        {
            if (CurrentPlayer.Power < RETURN_SPY_COST)
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

        private void ClearState()
        {
            CurrentState = ActionState.Normal;
            PendingCard = null;
            PendingSite = null;
            PendingMoveSource = null; // New cleanup
        }

        public void CancelTargeting()
        {
            ClearState();
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
                case ActionState.TargetingMoveSource:
                    HandleMoveSource(targetNode);
                    break;
                case ActionState.TargetingMoveDestination:
                    HandleMoveDestination(targetNode);
                    break;
            }
        }

        public void CompleteAction()
        {
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            ClearState();
        }

        private void HandleAssassinate(MapNode targetNode)
        {
            if (targetNode == null) return;

            if (!_mapManager.CanAssassinate(targetNode, CurrentPlayer))
            {
                OnActionFailed?.Invoke(this, "Invalid Target!");
                return;
            }

            if (PendingCard == null)
            {
                if (!CurrentPlayer.TrySpendPower(ASSASSINATE_COST))
                {
                    CancelTargeting();
                    OnActionFailed?.Invoke(this, $"Not enough Power to execute Assassinate! (Need {ASSASSINATE_COST})");
                    return;
                }
            }

            _mapManager.Assassinate(targetNode, CurrentPlayer);
            CompleteAction();
        }

        private void HandleReturn(MapNode targetNode)
        {
            if (targetNode == null) return;
            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, CurrentPlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral) return;

                _mapManager.ReturnTroop(targetNode, CurrentPlayer);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);

                // FIX: Automatically clean up state after success
                ClearState();
            }
        }

        private void HandleSupplant(MapNode targetNode)
        {
            if (targetNode == null) return;
            if (!_mapManager.CanAssassinate(targetNode, CurrentPlayer)) return;
            if (CurrentPlayer.TroopsInBarracks <= 0) return;

            _mapManager.Supplant(targetNode, CurrentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);

            // FIX: Automatically clean up state after success
            ClearState();
        }

        private void HandlePlaceSpy(Site targetSite)
        {
            if (targetSite == null) return;
            if (targetSite.Spies.Contains(CurrentPlayer.Color)) return;
            if (CurrentPlayer.SpiesInBarracks <= 0) return;

            _mapManager.PlaceSpy(targetSite, CurrentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);

            // FIX: Automatically clean up state after success
            ClearState();
        }

        private void HandleReturnSpyInitialClick(Site targetSite)
        {
            if (targetSite == null) return;

            if (PendingCard == null && CurrentPlayer.Power < RETURN_SPY_COST)
            {
                CancelTargeting();
                OnActionFailed?.Invoke(this, $"Not enough Power to execute Return Spy! (Need {RETURN_SPY_COST})");
                return;
            }

            var enemySpies = _mapManager.GetEnemySpiesAtSite(targetSite, CurrentPlayer);

            if (enemySpies.Count == 0)
            {
                OnActionFailed?.Invoke(this, "Invalid Target: No enemy spies here.");
                return;
            }

            var distinctEnemies = enemySpies.Distinct().ToList();

            if (distinctEnemies.Count == 1)
            {
                if (PendingCard == null)
                {
                    if (!CurrentPlayer.TrySpendPower(RETURN_SPY_COST))
                    {
                        CancelTargeting();
                        OnActionFailed?.Invoke(this, "Not enough Power!");
                        return;
                    }
                }

                bool success = _mapManager.ReturnSpecificSpy(targetSite, CurrentPlayer, distinctEnemies[0]);

                if (success)
                {
                    OnActionCompleted?.Invoke(this, EventArgs.Empty);
                    // FIX: Automatically clean up state after success
                    ClearState();
                }
            }
            else
            {
                // Do NOT clear state here, we need to wait for selection
                PendingSite = targetSite;
                CurrentState = ActionState.SelectingSpyToReturn;
                GameLogger.Log($"Multiple spies detected at {targetSite.Name}. Select which faction to return.", LogChannel.General);
            }
        }

        public void FinalizeSpyReturn(PlayerColor selectedSpyColor)
        {
            if (PendingSite == null) return;

            if (PendingCard == null)
            {
                if (!CurrentPlayer.TrySpendPower(RETURN_SPY_COST))
                {
                    CancelTargeting();
                    OnActionFailed?.Invoke(this, "Not enough Power!");
                    return;
                }
            }

            bool success = _mapManager.ReturnSpecificSpy(PendingSite, CurrentPlayer, selectedSpyColor);
            if (success)
            {
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                // FIX: Automatically clean up state after success
                ClearState();
            }
        }

        private void HandleMoveSource(MapNode targetNode)
        {
            if (targetNode == null) return;

            if (!_mapManager.CanMoveSource(targetNode, CurrentPlayer))
            {
                OnActionFailed?.Invoke(this, "Invalid Target: Must be an enemy troop where you have presence.");
                return;
            }

            PendingMoveSource = targetNode;
            CurrentState = ActionState.TargetingMoveDestination;
            GameLogger.Log("Select an empty destination space anywhere on the board.", LogChannel.General);
        }

        private void HandleMoveDestination(MapNode targetNode)
        {
            if (targetNode == null || PendingMoveSource == null) return;

            if (!_mapManager.CanMoveDestination(targetNode))
            {
                OnActionFailed?.Invoke(this, "Invalid Destination: Space must be empty.");
                return;
            }

            _mapManager.MoveTroop(PendingMoveSource, targetNode);
            CompleteAction();
        }

        public void TryStartDevourHand(Card sourceCard)
        {
            // Optional: Check if hand is empty?
            // If hand is empty, usually the effect fizzles or you just complete immediately.
            if (CurrentPlayer.Hand.Count == 0)
            {
                GameLogger.Log("No cards in hand to Devour.", LogChannel.Warning);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            StartTargeting(ActionState.TargetingDevourHand, sourceCard);
            GameLogger.Log("Select a card from your HAND to Devour (Remove from game).", LogChannel.General);
        }
    }
}