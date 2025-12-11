using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class ActionSystem
    {
        public ActionState CurrentState { get; private set; } = ActionState.Normal;
        public Card PendingCard { get; private set; }

        private readonly Player _activePlayer;
        private readonly MapManager _mapManager;

        public ActionSystem(Player activePlayer, MapManager mapManager)
        {
            _activePlayer = activePlayer;
            _mapManager = mapManager;
        }

        public void TryStartAssassinate()
        {
            const int cost = 3;
            if (_activePlayer.Power < cost)
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
            if (_activePlayer.Power < cost)
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
                    return HandleReturnSpy(targetSite);
                default:
                    return false;
            }
        }

        private bool HandleAssassinate(MapNode targetNode)
        {
            if (targetNode == null) return false;
            if (_mapManager.CanAssassinate(targetNode, _activePlayer))
            {
                if (PendingCard == null) _activePlayer.Power -= 3;
                _mapManager.Assassinate(targetNode, _activePlayer);
                return true;
            }
            GameLogger.Log("Invalid Target!", LogChannel.Error);
            return false;
        }

        private bool HandleReturn(MapNode targetNode)
        {
            if (targetNode == null) return false;
            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, _activePlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral) return false;
                _mapManager.ReturnTroop(targetNode, _activePlayer);
                return true;
            }
            return false;
        }

        private bool HandleSupplant(MapNode targetNode)
        {
            if (targetNode == null) return false;
            if (!_mapManager.CanAssassinate(targetNode, _activePlayer)) return false;
            if (_activePlayer.TroopsInBarracks <= 0) return false;
            _mapManager.Supplant(targetNode, _activePlayer);
            return true;
        }

        private bool HandlePlaceSpy(Site targetSite)
        {
            if (targetSite == null) return false;
            if (targetSite.Spies.Contains(_activePlayer.Color)) return false;
            if (_activePlayer.SpiesInBarracks <= 0) return false;
            _mapManager.PlaceSpy(targetSite, _activePlayer);
            return true;
        }

        private bool HandleReturnSpy(Site targetSite)
        {
            if (targetSite == null) return false;
            if (_mapManager.ReturnSpy(targetSite, _activePlayer))
            {
                if (PendingCard == null) _activePlayer.Power -= 3;
                return true;
            }
            return false;
        }
    }
}