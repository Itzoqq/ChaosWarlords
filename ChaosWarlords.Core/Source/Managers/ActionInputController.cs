using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Mechanics.Actions.Subsystems;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Converts raw input (map/site clicks) into specific Game Commands based on the current
    /// targeting state of the ActionSystem. Owns the click-to-command routing responsibility
    /// that ActionSystem previously handled internally, keeping ActionSystem focused on
    /// game logic and state management (SRP).
    /// </summary>
    public class ActionInputController
    {
        private readonly IActionSystem _actionSystem;
        private readonly IMapManager _mapManager;
        private readonly ISpySubsystem _spySubsystem;
        private readonly ITurnManager _turnManager;
        private readonly IGameLogger _logger;

        public ActionInputController(IActionSystem actionSystem, IMapManager mapManager, ISpySubsystem spySubsystem, ITurnManager turnManager, IGameLogger logger)
        {
            _actionSystem = actionSystem;
            _mapManager = mapManager;
            _spySubsystem = spySubsystem;
            _turnManager = turnManager;
            _logger = logger;
        }

        private Player ActivePlayer() => _turnManager.ActivePlayer;

        /// <summary>
        /// Routes a click on a map node/site to the appropriate handler for the current
        /// ActionSystem targeting state, returning the resulting command (if any).
        /// </summary>
        public IGameCommand? HandleTargetClick(MapNode? targetNode, Site? targetSite)
        {
            var state = _actionSystem.CurrentState;
            var pendingCardId = _actionSystem.PendingCard?.Id;
            var devourCardId = _actionSystem.PendingDevourCard?.Id;

            return state switch
            {
                ActionState.TargetingAssassinate => targetNode != null ? HandleAssassinate(targetNode, pendingCardId, devourCardId) : null,
                ActionState.TargetingReturn => targetNode != null ? HandleReturn(targetNode, pendingCardId) : null,
                ActionState.TargetingSupplant => targetNode != null ? HandleSupplant(targetNode, pendingCardId, devourCardId) : null,
                ActionState.TargetingPlaceSpy => targetSite != null ? _spySubsystem.HandlePlaceSpy(targetSite, pendingCardId) : null,
                ActionState.TargetingReturnSpy => targetSite != null ? _spySubsystem.HandleReturnSpyInitialClick(targetSite, pendingCardId) : null,
                ActionState.TargetingReturnOwnSpy => targetSite != null ? _spySubsystem.HandleReturnOwnSpy(targetSite, pendingCardId) : null,
                ActionState.TargetingMoveSource => targetNode != null ? HandleMoveSource(targetNode) : null,
                ActionState.TargetingMoveDestination => targetNode != null ? HandleMoveDestination(targetNode, pendingCardId) : null,
                _ => null,
            };
        }

        private AssassinateCommand? HandleAssassinate(MapNode targetNode, string? cardId, string? devourCardId)
        {
            if (!_mapManager.CanAssassinate(targetNode, ActivePlayer()))
            {
                _actionSystem.RaiseActionFailed("Invalid Target!");
                return null;
            }

            // Site-scoped Assassinate (e.g. Cloaker: "assassinate a troop at that spy's
            // site") - PendingSite is set by ReturnOwnSpyCommand right before this chains in,
            // and is null for every other Assassinate flow (cleared on every return to
            // Normal), so this never affects the normal, unscoped case.
            if (_actionSystem.PendingSite != null && !_actionSystem.PendingSite.NodesInternal.Contains(targetNode))
            {
                _actionSystem.RaiseActionFailed("Must assassinate at the site you returned your spy from.");
                return null;
            }

            if (string.IsNullOrEmpty(cardId) && ActivePlayer().Power < GameConstants.AssassinatePowerCost)
            {
                _actionSystem.NotifyFailure($"Not enough Power to execute Assassinate! (Need {GameConstants.AssassinatePowerCost})");
                return null;
            }

            return new AssassinateCommand(targetNode.Id, cardId, devourCardId);
        }

        private ReturnTroopCommand? HandleReturn(MapNode targetNode, string? cardId)
        {
            // Delegates to MapManager.CanReturnTroop - see ReturnTroopCommand.Validate's
            // comment for why this used to reimplement the same checks independently.
            if (!_mapManager.CanReturnTroop(targetNode, ActivePlayer()))
            {
                return null;
            }

            return new ReturnTroopCommand(targetNode.Id, cardId);
        }

        private SupplantCommand? HandleSupplant(MapNode targetNode, string? cardId, string? devourCardId)
        {
            if (!_mapManager.CanAssassinate(targetNode, ActivePlayer())) return null;
            if (ActivePlayer().TroopsInBarracks <= 0) return null;

            return new SupplantCommand(targetNode.Id, cardId, devourCardId);
        }

        private IGameCommand? HandleMoveSource(MapNode targetNode)
        {
            if (!_mapManager.CanMoveSource(targetNode, ActivePlayer()))
            {
                _actionSystem.RaiseActionFailed("Invalid Target: Must be an enemy troop where you have presence.");
                return null;
            }

            // Source selection is an intermediate step, not itself a command - it just
            // advances ActionSystem to the destination-targeting state.
            _actionSystem.SetMoveSource(targetNode);
            return null;
        }

        private MoveTroopCommand? HandleMoveDestination(MapNode targetNode, string? cardId)
        {
            var source = _actionSystem.PendingMoveSource;
            if (source == null) return null;

            if (!_mapManager.CanMoveDestination(targetNode))
            {
                _actionSystem.RaiseActionFailed("Invalid Destination: Space must be empty.");
                return null;
            }

            return new MoveTroopCommand(source.Id, targetNode.Id, cardId);
        }
    }
}
