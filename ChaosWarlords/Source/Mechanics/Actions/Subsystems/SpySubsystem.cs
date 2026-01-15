using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    public class SpySubsystem : ISpySubsystem
    {
        private readonly IMapManager _mapManager;
        private readonly ITurnManager _turnManager;
        private readonly IActionSystem _actionSystem;
        private readonly IGameLogger _logger;
        private IPlayerStateManager? _playerStateManager;

        public SpySubsystem(
            IMapManager mapManager,
            ITurnManager turnManager,
            IActionSystem actionSystem,
            IGameLogger logger)
        {
            _mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
            _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
        }

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public IGameCommand? HandlePlaceSpy(Site targetSite, string? cardId)
        {
            if (targetSite is null) return null;
            if (targetSite.Spies.Contains(CurrentPlayer.Color)) return null;
            if (CurrentPlayer.SpiesInBarracks <= 0) return null;

            return new Commands.PlaceSpyCommand(targetSite.Id, cardId);
        }

        public void PerformPlaceSpy(Site site, string? cardId)
        {
            _mapManager.PlaceSpy(site, CurrentPlayer);
            // ActionSystem completes the action
            _actionSystem.CompleteAction();
        }

        public IGameCommand? HandleReturnSpyInitialClick(Site clickedSite, string? cardId)
        {
            // 1. Sanity Checks
            if (clickedSite is null)
            {
                _logger.Log("Invalid Target: You must click a Site.", LogChannel.Warning);
                return null;
            }

            var enemySpies = _mapManager.GetEnemySpiesAtSite(clickedSite, CurrentPlayer);

            if (!IsValidSpyReturnTarget(clickedSite, enemySpies, cardId, out var failReason))
            {
                // We need to notify failure. ActionSystem has the event.
                // We can't fire the event directly on ActionSystem interface?
                // IActionSystem exposes specific methods, usually not "RaiseEvent".
                // But passing 'null' effectively stops the command generation.
                // We should probably log the reason or use a callback mechanism if we want UI feedback.
                // See ActionSystem internal implementation: OnActionFailed?.Invoke(...)
                // We can't invoke that event from outside.
                // Refactor opportunity: IActionSystem should have `NotifyFailure(string reason)`.

                // Refactor opportunity uses NotifyFailure.
                _actionSystem.NotifyFailure(failReason);
                return null;
            }

            return ExecuteReturnSpy(clickedSite, enemySpies, cardId);
        }

        private bool IsValidSpyReturnTarget(Site site, List<PlayerColor> enemySpies, string? cardId, out string reason)
        {
            if (enemySpies is null || enemySpies.Count == 0)
            {
                reason = "Target has no enemy spies.";
                return false;
            }

            // If cardId is null (not played by card), we check cost.
            if (string.IsNullOrEmpty(cardId) && CurrentPlayer.Power < GameConstants.ReturnSpyPowerCost)
            {
                reason = $"Not enough Power. Need {GameConstants.ReturnSpyPowerCost}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private IGameCommand? ExecuteReturnSpy(Site site, List<PlayerColor> enemySpies, string? cardId)
        {
            if (enemySpies.Count == 1)
            {
                // We can return immediately only if we know the spy color.
                // But wait, ExecuteReturnSpy in original code set PendingSite and returned FinalizeSpyReturn
                // OR set state to SelectingSpyToReturn.

                // We need to support state transitions.
                // If 1 spy: Immediate command generation.
                // If >1 spy: Transition to SelectingSpyToReturn state.

                // But wait, if we generate command here, who executes it?
                // The caller (ActionSystem.HandleTargetClick) -> AutoExecuteCommand.

                return FinalizeSpyReturn(enemySpies[0], site, cardId);
            }

            // Multiple spies: We need to change state.
            // We need to tell ActionSystem to buffer the Site and switch state.
            // ActionSystem has PendingSite. We can't set it directly via Interface?
            // Interface says: "Site? PendingSite { get; }" -> Read Only.

            // CHALLENGE: Subsystem cannot set ActionSystem state directly.
            // Solution: 
            // 1. ActionSystem exposes methods to SetPendingSite?
            // 2. Subsystem manages this state?
            // 3. Return a special "SwitchStateCommand"? No.

            // If we want good decoupling, Subsystem should manage the "Selection" phase logic.
            // But ActionSystem holds the "PendingSite".

            // Workaround: We cast to ActionSystem concrete (bad) or add setter to interface (good).
            // Or we handle state transition via a Side Effect method on IActionSystem.
            // "EnterSpySelectionState(Site site)"

            _logger.Log("Multiple spies detected. Select which spy to return.", LogChannel.General);

            // This requires ActionSystem support to store the site and switch state.
            // For now, I will assume we can add a method to IActionSystem later or cast.
            // Given I am modifying ActionSystem anyway, I will add `SetPendingSpyReturnSite(Site site)` to interface or similar.

            _actionSystem.TransitionToSpySelection(site);

            return null;
        }

        public IGameCommand? FinalizeSpyReturn(PlayerColor selectedSpyColor, Site pendingSite, string? cardId)
        {
            if (pendingSite is null) return null;

            if (!ValidateSpyReturn(CurrentPlayer, cardId)) return null;

            return new Commands.ResolveSpyCommand(pendingSite.Id, selectedSpyColor, cardId);
        }

        private bool ValidateSpyReturn(Player player, string? cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                if (player.Power < GameConstants.ReturnSpyPowerCost)
                {
                    _actionSystem.CancelTargeting();
                    // OnActionFailed...
                    return false;
                }
            }
            return true;
        }

        public bool PerformSpyReturn(Site site, PlayerColor selectedSpyColor, string? cardId)
        {
            // Logic
            bool success = _mapManager.ReturnSpecificSpy(site, CurrentPlayer, selectedSpyColor);

            if (success)
            {
                bool isPaidByCard = !string.IsNullOrEmpty(cardId);
                if (!isPaidByCard)
                {
                    if (_playerStateManager is not null)
                    {
                        _playerStateManager.TrySpendPower(CurrentPlayer, GameConstants.ReturnSpyPowerCost);
                    }
                    else
                    {
                        CurrentPlayer.Power -= GameConstants.ReturnSpyPowerCost;
                    }
                }

                _actionSystem.CompleteAction();
                return true;
            }
            else
            {
                _actionSystem.NotifyFailure("Map Manager failed to return spy.");
                return false;
            }
        }
    }
}
