using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class ActionSystem
    {
        public GameState CurrentState { get; private set; } = GameState.Normal;
        public Card PendingCard { get; private set; }

        private readonly Player _activePlayer;
        private readonly MapManager _mapManager;

        public ActionSystem(Player activePlayer, MapManager mapManager)
        {
            _activePlayer = activePlayer;
            _mapManager = mapManager;
        }

        public void StartTargeting(GameState state, Card card = null)
        {
            CurrentState = state;
            PendingCard = card;
            // Logging is handled by the caller (Game1)
        }

        public void CancelTargeting()
        {
            CurrentState = GameState.Normal;
            PendingCard = null;
            GameLogger.Log("Targeting Cancelled.", LogChannel.General);
        }

        public bool IsTargeting()
        {
            return CurrentState != GameState.Normal;
        }

        /// <summary>
        /// Attempts to resolve a targeting action based on the current state and mouse click.
        /// </summary>
        /// <returns>True if the action was successfully completed, allowing Game1 to finalize costs/card effects.</returns>
        public bool HandleTargetClick(MapNode targetNode, Site targetSite)
        {
            switch (CurrentState)
            {
                case GameState.TargetingAssassinate:
                    return HandleAssassinate(targetNode);
                case GameState.TargetingReturn:
                    return HandleReturn(targetNode);
                case GameState.TargetingSupplant:
                    return HandleSupplant(targetNode);
                case GameState.TargetingPlaceSpy:
                    return HandlePlaceSpy(targetSite);
                case GameState.TargetingReturnSpy:
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
                _mapManager.Assassinate(targetNode, _activePlayer);
                return true;
            }
            GameLogger.Log("Invalid Target! Need Presence or cannot target self/empty.", LogChannel.Error);
            return false;
        }

        private bool HandleReturn(MapNode targetNode)
        {
            if (targetNode == null) return false;

            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, _activePlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral)
                {
                    GameLogger.Log("Cannot return Neutral troops.", LogChannel.Error);
                    return false;
                }
                _mapManager.ReturnTroop(targetNode, _activePlayer);
                return true;
            }
            GameLogger.Log("Invalid Return Target.", LogChannel.Error);
            return false;
        }

        private bool HandleSupplant(MapNode targetNode)
        {
            if (targetNode == null) return false;

            if (!_mapManager.CanAssassinate(targetNode, _activePlayer))
            {
                // CanAssassinate already checks for valid enemy target and presence.
                return false;
            }

            if (_activePlayer.TroopsInBarracks <= 0)
            {
                GameLogger.Log("Barracks Empty!", LogChannel.Error);
                return false;
            }

            _mapManager.Supplant(targetNode, _activePlayer);
            return true;
        }

        private bool HandlePlaceSpy(Site targetSite)
        {
            if (targetSite == null) return false;

            if (targetSite.Spies.Contains(_activePlayer.Color))
            {
                GameLogger.Log("You already have a spy here.", LogChannel.Error);
                return false;
            }

            if (_activePlayer.SpiesInBarracks <= 0)
            {
                GameLogger.Log("No Spies in Barracks!", LogChannel.Error);
                return false;
            }

            _mapManager.PlaceSpy(targetSite, _activePlayer);
            return true;
        }

        private bool HandleReturnSpy(Site targetSite)
        {
            if (targetSite == null) return false;

            // ReturnSpy internally checks for presence and logs errors.
            return _mapManager.ReturnSpy(targetSite, _activePlayer);
        }
    }
}