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
        private readonly IPlayerStateManager _playerStateManager;

        public SpySubsystem(
            IMapManager mapManager,
            ITurnManager turnManager,
            IActionSystem actionSystem,
            IGameLogger logger,
            IPlayerStateManager playerStateManager)
        {
            _mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
            _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerStateManager = playerStateManager ?? throw new ArgumentNullException(nameof(playerStateManager));
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

            // Must be set before CompleteAction() - see PlaceSpyCommand.Execute's matching
            // comment (same requirement, this is the secondary direct-call entry point).
            _actionSystem.SetPendingSiteForChain(site);

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
                _actionSystem.NotifyFailure(failReason);
                return null;
            }

            return ExecuteReturnSpy(clickedSite, enemySpies, cardId);
        }

        /// <summary>
        /// Handles a site click while returning ONE OF THE ACTIVE PLAYER'S OWN spies (e.g.
        /// Cloaker). Simpler than the enemy-spy flow: a player only ever has at most one spy
        /// of their own color at a given site, so there's no multi-color "which spy"
        /// sub-step (SelectingSpyToReturn) and no Power cost (card-effect-driven, not the
        /// paid base action).
        /// </summary>
        public IGameCommand? HandleReturnOwnSpy(Site clickedSite, string? cardId)
        {
            if (clickedSite is null)
            {
                _logger.Log("Invalid Target: You must click a Site.", LogChannel.Warning);
                return null;
            }

            if (!_mapManager.CanReturnOwnSpy(clickedSite, CurrentPlayer))
            {
                _actionSystem.NotifyFailure("You have no spy at that site.");
                return null;
            }

            return new Commands.ReturnOwnSpyCommand(clickedSite.Id, cardId);
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
                // Only one candidate - resolve immediately, no need to ask which spy.
                return FinalizeSpyReturn(enemySpies[0], site, cardId);
            }

            // Multiple enemy spies at this site: buffer the site and switch ActionSystem into
            // SelectingSpyToReturn so the next click picks which color to return.
            _logger.Log("Multiple spies detected. Select which spy to return.", LogChannel.General);
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
                    // Presently unreachable in practice - Power can't change between the
                    // initial site click (already gated by IsValidSpyReturnTarget's identical
                    // check) and this follow-up spy-color click within one synchronous
                    // targeting sequence. Kept as a real guard (not just an assert) in case
                    // that assumption ever stops holding, matching IsValidSpyReturnTarget's own
                    // NotifyFailure rather than cancelling silently.
                    _actionSystem.NotifyFailure($"Not enough Power. Need {GameConstants.ReturnSpyPowerCost}.");
                    _actionSystem.CancelTargeting();
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
                    _playerStateManager.TrySpendPower(CurrentPlayer, GameConstants.ReturnSpyPowerCost);
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
