using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using System;

namespace ChaosWarlords.Source.Managers
{
    public class ActionSystem : IActionSystem
    {
        // Logic Constants
        private const int ASSASSINATE_COST = GameConstants.AssassinatePowerCost;
        private const int RETURN_SPY_COST = GameConstants.ReturnSpyPowerCost;

        // Event Definitions
        public event EventHandler? OnActionCompleted;
        public event EventHandler<string>? OnActionFailed;

        public ActionState CurrentState { get; internal set; } = ActionState.Normal;
        public Card? PendingCard { get; internal set; }
        public Site? PendingSite { get; private set; }

        private readonly ITurnManager _turnManager;
        private readonly IMapManager _mapManager;
        private readonly IGameLogger _logger;
        private IPlayerStateManager _playerStateManager = null!;

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public MapNode? PendingMoveSource { get; private set; }

        public ActionSystem(ITurnManager turnManager, IMapManager mapManager, IGameLogger logger)
        {
            _turnManager = turnManager;
            _mapManager = mapManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
        }

        public void TryStartAssassinate()
        {
            if (CurrentPlayer.Power < ASSASSINATE_COST)
            {
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {ASSASSINATE_COST}.");
                return;
            }

            StartTargeting(ActionState.TargetingAssassinate);
            _logger.Log($"Select a TROOP to Assassinate (Cost: {ASSASSINATE_COST} Power)...", LogChannel.General);
        }

        public void TryStartReturnSpy()
        {
            if (CurrentPlayer.Power < RETURN_SPY_COST)
            {
                OnActionFailed?.Invoke(this, $"Not enough Power! Need {RETURN_SPY_COST}.");
                return;
            }

            StartTargeting(ActionState.TargetingReturnSpy);
            _logger.Log($"Select a SITE to remove Enemy Spy (Cost: {RETURN_SPY_COST} Power)...", LogChannel.General);
        }

        public void StartTargeting(ActionState state, Card? card = null)
        {
            CurrentState = state;
            PendingCard = card;
        }

        private void ClearState()
        {
            CurrentState = ActionState.Normal;
            PendingCard = null;
            PendingSite = null;
            PendingMoveSource = null;
        }

        public void CancelTargeting()
        {
            ClearState();
            _logger.Log("ActionSystem: Targeting Cancelled. State cleared.", LogChannel.Info);
        }

        public bool IsTargeting()
        {
            return CurrentState != ActionState.Normal;
        }

        public IGameCommand? HandleTargetClick(MapNode? targetNode, Site? targetSite)
        {
            return CurrentState switch
            {
                ActionState.TargetingAssassinate when targetNode is not null => HandleAssassinate(targetNode),
                ActionState.TargetingReturn when targetNode is not null => HandleReturn(targetNode),
                ActionState.TargetingSupplant when targetNode is not null => HandleSupplant(targetNode),
                ActionState.TargetingPlaceSpy when targetSite is not null => HandlePlaceSpy(targetSite),
                ActionState.TargetingReturnSpy when targetSite is not null => HandleReturnSpyInitialClick(targetSite),
                ActionState.TargetingMoveSource when targetNode is not null => HandleMoveSource(targetNode),
                ActionState.TargetingMoveDestination when targetNode is not null => HandleMoveDestination(targetNode),
                _ => null
            };
        }

        public void CompleteAction()
        {
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            ClearState();
        }

        // --- Commands Implementation ---

        private AssassinateCommand? HandleAssassinate(MapNode targetNode)
        {
            if (targetNode is null) return null;
            if (!ValidateAssassinate(targetNode)) return null;

            return new ChaosWarlords.Source.Commands.AssassinateCommand(targetNode.Id, PendingCard?.Id);
        }
        
        // Removed Record and direct call. Coordinator will execute.

        public void PerformAssassinate(MapNode node, string? cardId)
        {
            // Re-validation for Replay safety? 
            // Replays assume valid input, but sanity check doesn't hurt.
            // However, cost check must handle "paid by card".
            
            // Logic: Spend cost (if not by card) -> Execute.
            
            bool isPaidByCard = !string.IsNullOrEmpty(cardId);

            if (!isPaidByCard)
            {
                SpendAssassinateCost();
            }

            _mapManager.Assassinate(node, CurrentPlayer);
            CompleteAction();
        }

        // Renaming/Refactoring done. Removed old ExecuteAssassinate to avoid confusion.

        private bool ValidateAssassinate(MapNode targetNode)
        {
            if (!_mapManager.CanAssassinate(targetNode, CurrentPlayer))
            {
                OnActionFailed?.Invoke(this, "Invalid Target!");
                return false;
            }

            if (PendingCard is null && CurrentPlayer.Power < ASSASSINATE_COST)
            {
                CancelTargeting();
                OnActionFailed?.Invoke(this, $"Not enough Power to execute Assassinate! (Need {ASSASSINATE_COST})");
                return false;
            }

            return true;
        }

        private void ExecuteAssassinate(MapNode targetNode)
        {
            if (PendingCard is null)
            {
                SpendAssassinateCost();
            }

            _mapManager.Assassinate(targetNode, CurrentPlayer);
            CompleteAction();
        }

        private void SpendAssassinateCost()
        {
            if (_playerStateManager is not null)
            {
                _playerStateManager.TrySpendPower(CurrentPlayer, ASSASSINATE_COST);
            }
            else
            {
                CurrentPlayer.Power -= ASSASSINATE_COST;
            }
        }

        private ReturnTroopCommand? HandleReturn(MapNode targetNode)
        {
            if (targetNode is null) return null;
            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, CurrentPlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral) return null;

                return new ChaosWarlords.Source.Commands.ReturnTroopCommand(targetNode.Id, PendingCard?.Id);
            }
            return null;
        }

        public void PerformReturnTroop(MapNode node, string? cardId)
        {
            _mapManager.ReturnTroop(node, CurrentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            ClearState();
        }

        private SupplantCommand? HandleSupplant(MapNode targetNode)
        {
            if (targetNode is null) return null;
            if (!_mapManager.CanAssassinate(targetNode, CurrentPlayer)) return null;
            if (CurrentPlayer.TroopsInBarracks <= 0) return null;

            return new ChaosWarlords.Source.Commands.SupplantCommand(targetNode.Id, PendingCard?.Id);
        }

        public void PerformSupplant(MapNode node, string? cardId)
        {
            _mapManager.Supplant(node, CurrentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            ClearState();
        }

        private PlaceSpyCommand? HandlePlaceSpy(Site targetSite)
        {
            if (targetSite is null) return null;
            if (targetSite.Spies.Contains(CurrentPlayer.Color)) return null;
            if (CurrentPlayer.SpiesInBarracks <= 0) return null;

            return new ChaosWarlords.Source.Commands.PlaceSpyCommand(targetSite.Id, PendingCard?.Id);
        }

        public void PerformPlaceSpy(Site site, string? cardId)
        {
            _mapManager.PlaceSpy(site, CurrentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            ClearState();
        }

        private IGameCommand? HandleReturnSpyInitialClick(Site clickedSite)
        {
            // 1. Sanity Checks
            if (clickedSite is null)
            {
                _logger.Log("Invalid Target: You must click a Site.", LogChannel.Warning);
                return null;
            }

            var enemySpies = _mapManager.GetEnemySpiesAtSite(clickedSite, CurrentPlayer);

            if (!IsValidSpyReturnTarget(clickedSite, enemySpies, out var failReason))
            {
                OnActionFailed?.Invoke(this, failReason);
                return null;
            }

            // 3. Execution (Returns Command or enters sub-state)
            return ExecuteReturnSpy(clickedSite, enemySpies);
        }
        
        private bool IsValidSpyReturnTarget(Site site, System.Collections.Generic.List<PlayerColor> enemySpies, out string reason)
        {
            if (enemySpies is null || enemySpies.Count == 0)
            {
                reason = "Target has no enemy spies.";
                return false;
            }

            if (PendingCard is null && CurrentPlayer.Power < RETURN_SPY_COST)
            {
                reason = $"Not enough Power. Need {RETURN_SPY_COST}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private IGameCommand? ExecuteReturnSpy(Site site, System.Collections.Generic.List<PlayerColor> enemySpies)
        {
            if (enemySpies.Count == 1)
            {
                PendingSite = site;
                return FinalizeSpyReturn(enemySpies[0]);
            }

            PendingSite = site;
            CurrentState = ActionState.SelectingSpyToReturn;
            _logger.Log("Multiple spies detected. Select which spy to return.", LogChannel.General);
            return null;
        }

        public IGameCommand? FinalizeSpyReturn(PlayerColor selectedSpyColor)
        {
            if (PendingSite is null) return null;

            if (!ValidateSpyReturn(CurrentPlayer)) return null;

            return new ChaosWarlords.Source.Commands.ResolveSpyCommand(((ChaosWarlords.Source.Entities.Map.Site)PendingSite).Id, selectedSpyColor, PendingCard?.Id);
        }
        
        // Removed private ExecuteSpyReturn, it was redundant once ResolveSpyCommand takes over.

        private bool ValidateSpyReturn(Player player)
        {
            // Cost Check
            if (PendingCard is null)
            {
                if (player.Power < RETURN_SPY_COST)
                {
                    CancelTargeting();
                    OnActionFailed?.Invoke(this, "Not enough Power!");
                    return false;
                }
            }
            return true;
        }

        // Helper for ResolveSpyCommand to call back into
        public void ExecuteSpyReturn(Site site, PlayerColor selectedSpyColor)
        {
            PerformSpyReturn(site, selectedSpyColor, PendingCard?.Id);
        }
        
        // Renamed to match others, but not recording yet
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
                        _playerStateManager.TrySpendPower(CurrentPlayer, RETURN_SPY_COST);
                    }
                    else
                    {
                         CurrentPlayer.Power -= RETURN_SPY_COST;
                    }
                }
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                ClearState();
                return true;
            }
            else
            {
                OnActionFailed?.Invoke(this, "Action Failed: Invalid Target or Conditions.");
                return false;
            }
        }

        private IGameCommand? HandleMoveSource(MapNode targetNode)
        {
            if (targetNode is null) return null;

            if (!_mapManager.CanMoveSource(targetNode, CurrentPlayer))
            {
                OnActionFailed?.Invoke(this, "Invalid Target: Must be an enemy troop where you have presence.");
                return null;
            }

            PendingMoveSource = targetNode;
            CurrentState = ActionState.TargetingMoveDestination;
            _logger.Log("Select an empty destination space anywhere on the board.", LogChannel.General);
            return null;
        }

        private MoveTroopCommand? HandleMoveDestination(MapNode targetNode)
        {
            if (targetNode is null || PendingMoveSource is null) return null;

            if (!_mapManager.CanMoveDestination(targetNode))
            {
                OnActionFailed?.Invoke(this, "Invalid Destination: Space must be empty.");
                return null;
            }

            return new ChaosWarlords.Source.Commands.MoveTroopCommand(PendingMoveSource.Id, targetNode.Id, PendingCard?.Id);
        }

        public void PerformMoveTroop(MapNode source, MapNode dest, string? cardId)
        {
            _mapManager.MoveTroop(source, dest, CurrentPlayer);
            CompleteAction();
        }

        public void TryStartDevourHand(Card sourceCard)
        {
            if (CurrentPlayer.Hand.Count == 0)
            {
                _logger.Log("No cards in hand to Devour.", LogChannel.Warning);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            StartTargeting(ActionState.TargetingDevourHand, sourceCard);
            _logger.Log("Select a card from your HAND to Devour (Remove from game).", LogChannel.General);
        }
    }
}


