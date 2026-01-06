using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Actions.Subsystems; // Implementations remain here
using ChaosWarlords.Source.Commands; // Retained from original

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
        public event EventHandler<ActionState>? OnStateChanged;
        public event Action<IGameCommand>? OnAutoExecuteCommand;

        private ActionState _currentState = ActionState.Normal;
        public ActionState CurrentState 
        { 
            get => _currentState; 
            internal set 
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    OnStateChanged?.Invoke(this, _currentState);
                }
            }
        }
        public Card? PendingCard { get; private set; }

        private readonly Dictionary<Card, Dictionary<ActionState, object>> _preSelectedTargets = new();

        public void SetPreTarget(Card source, ActionState forState, object target)
        {
            if (!_preSelectedTargets.ContainsKey(source))
                _preSelectedTargets[source] = new Dictionary<ActionState, object>();

            _preSelectedTargets[source][forState] = target;
            _logger.Log($"ActionSystem: SetPreTarget for {source.Name} [{forState}]. Target: {target}", LogChannel.Debug);
        }

        public object? GetAndClearPreTarget(Card source, ActionState forState)
        {
            if (_preSelectedTargets.TryGetValue(source, out var stateTargets))
            {
                if (stateTargets.TryGetValue(forState, out var target))
                {
                    stateTargets.Remove(forState);
                    if (stateTargets.Count == 0) _preSelectedTargets.Remove(source);
                    
                    _logger.Log($"ActionSystem: GetAndClear Found target for {source.Name} [{forState}]", LogChannel.Debug);
                    return target;
                }
            }
            return null;
        }
        public Site? PendingSite { get; private set; }
        

        
        public Card? PendingDevourCard => _devourSubsystem.PendingDevourCard;
        // _deferDevourExecution moved to Subsystem

        private readonly ITurnManager _turnManager;
        private readonly IMapManager _mapManager;
        private readonly IGameLogger _logger;
        private IPlayerStateManager _playerStateManager = null!;

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public MapNode? PendingMoveSource { get; private set; }
        
        // Subsystems
        private readonly DevourSubsystem _devourSubsystem;
        private readonly SpySubsystem _spySubsystem;

        public ActionSystem(ITurnManager turnManager, IMapManager mapManager, IGameLogger logger)
        {
            _turnManager = turnManager;
            _mapManager = mapManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize Subsystems
            _devourSubsystem = new ChaosWarlords.Source.Mechanics.Actions.Subsystems.DevourSubsystem(_turnManager, this, _logger);
            _spySubsystem = new ChaosWarlords.Source.Mechanics.Actions.Subsystems.SpySubsystem(_mapManager, _turnManager, this, _logger);
            
            _actionHandlers = new Dictionary<ActionState, Func<MapNode?, Site?, IGameCommand?>>();
            InitializeHandlers();
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
            _devourSubsystem.SetPlayerStateManager(stateManager);
            // SpySubsystem uses PlayerStateManager via ActionSystem? No, it has its own logic that might need it?
            // Checking SpySubsystem: It has SetPlayerStateManager.
            if (_spySubsystem is ChaosWarlords.Source.Mechanics.Actions.Subsystems.SpySubsystem concreteSpy)
                concreteSpy.SetPlayerStateManager(stateManager);
        }

        private IMatchManager _matchManager = null!;
        private IMarketManager _marketManager = null!;

        public void SetMatchManager(IMatchManager matchManager)
        {
            _matchManager = matchManager;
            _devourSubsystem.SetMatchManager(matchManager);
        }

        public void SetMarketManager(IMarketManager marketManager)
        {
            _marketManager = marketManager;
            _devourSubsystem.SetMarketManager(marketManager);
        }

        private IMarketStateManager _marketStateManager = null!;
        public void SetMarketStateManager(IMarketStateManager manager)
        {
            _marketStateManager = manager;
            _devourSubsystem.SetMarketStateManager(manager);
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

            // Auto-Execute if Pre-Target exists (Transactional/Replay Flow)
            if (card != null && _preSelectedTargets.TryGetValue(card, out var stateTargets))
            {
                if (stateTargets.TryGetValue(state, out var target))
                {
                    _logger.Log($"StartTargeting: Pre-Target found for {state}. Auto-executing...", LogChannel.Info);
                    
                    // CRITICAL FIX: Consume the target immediately to prevent "zombie" targets if the card is replayed later
                    stateTargets.Remove(state);
                    if (stateTargets.Count == 0) _preSelectedTargets.Remove(card);

                    // Special Case: Devour is handled via TryStartDevourHand usually, but if we get here with state...
                    if (state == ActionState.TargetingDevourHand)
                    {
                         // If we are here, it means we want to execute the Devour logic via "Target".
                         // BUT Devour logic is unique (Cost).
                         // Let's defer to TryStartDevourHand? No, infinite loop risk.
                         // Actually, HandleDevourSelection(target as Card) is better.
                         if (target is Card c) HandleDevourSelection(c);
                         else if (target == SkippedTarget) HandleDevourSelection(null);
                         
                         // Note: HandleDevourSelection calls CompleteAction/ClearState if immediate, or buffers if deferred.
                         // If deferred, we stay in state? No, AdvanceTargeting moves state.
                         return;
                    }

                    if (target is MapNode node)
                    {
                        var cmd = HandleTargetClick(node, null);
                        if (cmd != null) OnAutoExecuteCommand?.Invoke(cmd);
                        return;
                    }
                    if (target is Site site)
                    {
                        var cmd = HandleTargetClick(null, site);
                        if (cmd != null) OnAutoExecuteCommand?.Invoke(cmd);
                        return;
                    }
                    // Handle other types if needed (e.g. Card for Promote? Promote uses logic)
                }
            }
        }

        private void ClearState()
        {
            CurrentState = ActionState.Normal;
            PendingCard = null;
            PendingSite = null;
            PendingMoveSource = null;
            // Note: PendingDevourCard is NOT cleared here to allow transactional persistence across chained actions.
        }

        public void NotifyFailure(string reason)
        {
             _logger.Log($"Action Failed: {reason}", LogChannel.Warning);
             // Verify if we should CancelTargeting here or just notify?
             // Usually failure implies resetting state or at least notifying UI.
             // CancelTargeting() clears state.
             CancelTargeting(); 
             OnActionFailed?.Invoke(this, reason);
        }

        public void CancelTargeting()
        {
            // Clear Pre-Selected targets to prevent "Zombie" executions if we restart
            if (PendingCard != null && _preSelectedTargets.ContainsKey(PendingCard))
            {
                _preSelectedTargets.Remove(PendingCard);
                _logger.Log($"Cleared Pre-Targets for {PendingCard.Name} due to Cancellation.", LogChannel.Debug);
            }

            ClearState();
            _devourSubsystem.ClearState();
            _logger.Log("ActionSystem: Targeting Cancelled. State cleared.", LogChannel.Info);
        }

        public bool IsTargeting()
        {
            return CurrentState != ActionState.Normal;
        }

        private readonly Dictionary<ActionState, Func<MapNode?, Site?, IGameCommand?>> _actionHandlers;

        private void InitializeHandlers()
        {
            _actionHandlers.Add(ActionState.TargetingAssassinate, (n, s) => n != null ? HandleAssassinate(n) : null);
            _actionHandlers.Add(ActionState.TargetingReturn, (n, s) => n != null ? HandleReturn(n) : null);
            _actionHandlers.Add(ActionState.TargetingSupplant, (n, s) => n != null ? HandleSupplant(n) : null);
            _actionHandlers.Add(ActionState.TargetingPlaceSpy, (n, s) => s != null ? _spySubsystem.HandlePlaceSpy(s, PendingCard?.Id) : null);
            _actionHandlers.Add(ActionState.TargetingReturnSpy, (n, s) => s != null ? _spySubsystem.HandleReturnSpyInitialClick(s, PendingCard?.Id) : null);
            _actionHandlers.Add(ActionState.TargetingMoveSource, (n, s) => n != null ? HandleMoveSource(n) : null);
            _actionHandlers.Add(ActionState.TargetingMoveDestination, (n, s) => n != null ? HandleMoveDestination(n) : null);
        }

        public IGameCommand? HandleTargetClick(MapNode? targetNode, Site? targetSite)
        {
            if (_actionHandlers.TryGetValue(CurrentState, out var handler))
            {
                return handler(targetNode, targetSite);
            }
            return null;
        }


        // --- Commands Implementation ---

        private AssassinateCommand? HandleAssassinate(MapNode targetNode)
        {
            if (targetNode is null) return null;
            if (!ValidateAssassinate(targetNode)) return null;

            return new ChaosWarlords.Source.Commands.AssassinateCommand(targetNode.Id, PendingCard?.Id, PendingDevourCard?.Id);
        }
        
        public void PerformAssassinate(MapNode node, string? cardId, string? devourCardId = null)
        {
            // Transactional Devour Handling (Logic Layer)
            if (!string.IsNullOrEmpty(devourCardId))
            {
                var cardToDevour = CurrentPlayer.Hand.FirstOrDefault(c => c.Id == devourCardId);
                if (cardToDevour != null) _matchManager.DevourCard(cardToDevour);
            }

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

            return new ChaosWarlords.Source.Commands.SupplantCommand(targetNode.Id, PendingCard?.Id, PendingDevourCard?.Id);
        }

        public void PerformSupplant(MapNode node, string? cardId, string? devourCardId = null)
        {
            // Transactional Devour Handling (Logic Layer)
            if (!string.IsNullOrEmpty(devourCardId))
            {
                var cardToDevour = CurrentPlayer.Hand.FirstOrDefault(c => c.Id == devourCardId);
                if (cardToDevour != null) _matchManager.DevourCard(cardToDevour);
                // Also check PendingDevourCard and clear it if it matches?
                // CompleteAction will clear state anyway.
            }
            else if (PendingDevourCard != null)
            {
                // Fallback: If not passed explicitly but exists in state (Deferred flow)
                _matchManager.DevourCard(PendingDevourCard);
            }

            _mapManager.Supplant(node, CurrentPlayer);
            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            ClearState();
        }

        public void TryStartSupplant(Card sourceCard)
        {
            // Check Pre-Target
            var preTarget = GetAndClearPreTarget(sourceCard, ActionState.TargetingSupplant);
            if (preTarget is int targetNodeId) 
            {
                 // Retrieve node by ID
                 var retrievedNode = _mapManager.Nodes.FirstOrDefault(n => n.Id == targetNodeId);
                 if (retrievedNode != null)
                 {
                     _logger.Log($"Supplant Pre-Target found (ID): Node {retrievedNode.Id}. Executing...", LogChannel.Info);
                     PerformSupplant(retrievedNode, sourceCard.Id);
                     return;
                 }
            }
            if (preTarget is MapNode node)
            {
                _logger.Log($"Supplant Pre-Target found (Object): Node {node.Id}. Executing...", LogChannel.Info);
                PerformSupplant(node, sourceCard.Id);
                return;
            }

            // Normal Flow
            bool canAssassinate = _mapManager.HasValidAssassinationTarget(CurrentPlayer);
            bool hasTroops = CurrentPlayer.TroopsInBarracks > 0;

            if (canAssassinate && hasTroops)
            {
                StartTargeting(ActionState.TargetingSupplant, sourceCard);
                _logger.Log($"{sourceCard.Name}: Initiating Supplant targeting. Select a valid target.", LogChannel.Input);
            }
            else
            {
               if (!hasTroops) _logger.Log($"{sourceCard.Name}: Cannot Supplant (No Troops in Barracks).", LogChannel.Warning);
               else _logger.Log($"{sourceCard.Name}: No valid targets to Supplant.", LogChannel.Warning);
            }
        }

        public bool AdvancePreCommitTargeting(Card sourceCard)
        {
            // Determine if the *current* step was skipped by the user, so the Engine knows to skip its children.
            bool isCurrentSkipped = IsPreTargetSkipped(sourceCard, CurrentState);

            var nextState = ChaosWarlords.Source.Mechanics.Rules.TargetingStateEngine.DetermineNextState(sourceCard.Effects, CurrentState, isCurrentSkipped);
            
            if (nextState != ActionState.Normal)
            {
                StartTargeting(nextState, sourceCard);
                _logger.Log($"Advancing Pre-Commit Targeting to {nextState}...", LogChannel.Info);
                return true; 
            }

            // No more targeting steps.
            _logger.Log($"Pre-Commit Targeting Complete for {sourceCard.Name}.", LogChannel.Info);
            ClearState(); 
            return false; 
        }

        private bool IsPreTargetSkipped(Card source, ActionState state)
        {
             if (_preSelectedTargets.TryGetValue(source, out var stateTargets))
             {
                 if (stateTargets.TryGetValue(state, out var target))
                 {
                     return target == SkippedTarget;
                 }
             }
             return false;
        }

        public void PerformPlaceSpy(Site site, string? cardId)
        {
            _spySubsystem.PerformPlaceSpy(site, cardId); // Completes Action internally
        }

        public IGameCommand? FinalizeSpyReturn(PlayerColor selectedSpyColor)
        {
             if (PendingSite is null) return null;
             // We need to pass PendingSite to subsystem or let subsystem manage it?
             // Subsystem stateless methods are better.
             return _spySubsystem.FinalizeSpyReturn(selectedSpyColor, PendingSite, PendingCard?.Id);
        }

        public bool PerformSpyReturn(Site site, PlayerColor selectedSpyColor, string? cardId)
        {
             return _spySubsystem.PerformSpyReturn(site, selectedSpyColor, cardId);
        }

        // Support for SpySubsystem State Transition
        public void TransitionToSpySelection(Site site)
        {
            PendingSite = site;
            CurrentState = ActionState.SelectingSpyToReturn;
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

        public static readonly object SkippedTarget = new object();

        public void TryStartDevourHand(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            _devourSubsystem.TryStartDevourHand(sourceCard, onComplete, deferExecution);
        }

        public void TryStartDevourMarket(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            _devourSubsystem.TryStartDevourMarket(sourceCard, onComplete, deferExecution);
        }

        public void TryStartDevourDeck(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            _devourSubsystem.TryStartDevourDeck(sourceCard, onComplete, deferExecution);
        }

        public void HandleDevourMarketSelection(Card? targetCard)
        {
            _devourSubsystem.HandleDevourMarketSelection(targetCard);
        }

        public void HandleDevourSelection(Card? targetCard)
        {
            _devourSubsystem.HandleDevourSelection(targetCard);
        }
        public void CompleteAction()
        {
            // Fix: Clear state FIRST. 
            // Previous order (Event -> Clear) caused any state set by Event Handlers (e.g. recursive PlayCard) 
            // to be immediately wiped by ClearState.
            ClearState();

            _logger.Log("ActionSystem: CompleteAction - State cleared. Invoking events/callbacks.", LogChannel.Debug);

            OnActionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}


