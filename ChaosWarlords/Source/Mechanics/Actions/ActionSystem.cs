using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

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
        
        public Card? PendingDevourCard { get; private set; }
        private bool _deferDevourExecution;

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
            
            _actionHandlers = new Dictionary<ActionState, Func<MapNode?, Site?, IGameCommand?>>();
            InitializeHandlers();
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
        }

        private IMatchManager _matchManager = null!;

        public void SetMatchManager(IMatchManager matchManager)
        {
            _matchManager = matchManager;
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

        public void CancelTargeting()
        {
            // Clear Pre-Selected targets to prevent "Zombie" executions if we restart
            if (PendingCard != null && _preSelectedTargets.ContainsKey(PendingCard))
            {
                _preSelectedTargets.Remove(PendingCard);
                _logger.Log($"Cleared Pre-Targets for {PendingCard.Name} due to Cancellation.", LogChannel.Debug);
            }

            ClearState();
            PendingDevourCard = null;
            _deferDevourExecution = false;
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
            _actionHandlers.Add(ActionState.TargetingPlaceSpy, (n, s) => s != null ? HandlePlaceSpy(s) : null);
            _actionHandlers.Add(ActionState.TargetingReturnSpy, (n, s) => s != null ? HandleReturnSpyInitialClick(s) : null);
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
                _logger.Log($"{sourceCard.Name}: Select a valid target to Supplant.", LogChannel.Input);
            }
            else
            {
               if (!hasTroops) _logger.Log($"{sourceCard.Name}: Cannot Supplant (No Troops in Barracks).", LogChannel.Warning);
               else _logger.Log($"{sourceCard.Name}: No valid targets to Supplant.", LogChannel.Warning);
            }
        }

        public bool AdvancePreCommitTargeting(Card sourceCard)
        {
            var nextState = FindNextTargetingState(sourceCard.Effects, CurrentState, sourceCard, out bool foundCurrent);
            
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

        private ActionState FindNextTargetingState(IEnumerable<CardEffect> effects, ActionState currentState, Card sourceCard, out bool foundCurrent)
        {
            foundCurrent = false;

            if (effects == null) return ActionState.Normal;

            foreach (var effect in effects)
            {
                var effectState = ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.GetTargetingState(effect.Type);
                
                // 1. Have we found the current state yet?
                if (!foundCurrent)
                {
                    if (effectState == currentState)
                    {
                        foundCurrent = true;
                        
                        // We found the current effect. Now determine path based on PreTarget.
                        // Check if we have a Pre-Target recorded
                        // Note: We check Dictionary directly to avoid clearing it
                        
                        // BUT, for now let's assume if "SkippedTarget" is set, we skip children.
                        bool isSkipped = IsPreTargetSkipped(sourceCard, currentState);
                        
                        if (isSkipped)
                        {
                            // Skip children (OnSuccess), continue to next sibling
                            continue;
                        }
                        else
                        {
                            // Targeted (or not set yet? assume set if we are here).
                            // Descend into OnSuccess (Depedency)
                            if (effect.OnSuccess != null)
                            {
                                bool foundInChild;
                                var childResult = FindNextTargetingState(new[] { effect.OnSuccess }, currentState, sourceCard, out foundInChild);
                                // If we just found current in parent, foundInChild is false (we passed currentState down? No)
                                // Wait, recursion logic is tricky. 
                                // Reset logic: We found 'current' HERE. So for the child call, 'foundCurrent' matches 'true'.
                                // effectively we are looking for the *Next* state inside the child.
                                
                                // Actually, simpler:
                                // If we found current, we immediately look for NEXT.
                                // Next candidates: 
                                // 1. OnSuccess (if not skipped)
                                // 2. Next Sibling
                                
                                // Look in Child
                                var nextInChild = FindTargetingStateRecursive(effect.OnSuccess);
                                if (nextInChild != ActionState.Normal) return nextInChild;
                                
                                continue; // Look at next sibling
                            }
                        }
                    }
                    else
                    {
                        // Not current. Search Children.
                        if (effect.OnSuccess != null)
                        {
                            var childState = FindNextTargetingState(new[] { effect.OnSuccess }, currentState, sourceCard, out foundCurrent);
                            if (foundCurrent)
                            {
                                // Current was found in child. The function returned the Next state if found.
                                if (childState != ActionState.Normal) return childState;
                                // If child finished, we continue to siblings
                            }
                        }
                    }
                }
                else
                {
                    // 2. We have passed the current state (foundCurrent is true).
                    // This effect is a candidate for "Next".
                    if (ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.IsTargetingEffect(effect.Type))
                    {
                        return effectState;
                    }
                    
                    // Not a targeting effect? Check its children.
                    if (effect.OnSuccess != null)
                    {
                        var childState = FindNextTargetingState(new[] { effect.OnSuccess }, currentState, sourceCard, out bool dummy);
                        // We already found current, so we treat child search as "Find ANY targeting state"
                        // Actually, reusing the method is complex.
                        // Let's use a simpler "FindFirstTargeting" for this branch.
                        var nextInChild = FindTargetingStateRecursive(effect.OnSuccess);
                        if (nextInChild != ActionState.Normal) return nextInChild;
                    }
                }
            }
            
            return ActionState.Normal;
        }

        private static ActionState FindTargetingStateRecursive(CardEffect? effect)
        {
            if (effect == null) return ActionState.Normal;

            if (ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.IsTargetingEffect(effect.Type))
            {
                return ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.GetTargetingState(effect.Type);
            }

            if (effect.OnSuccess != null)
            {
                return FindTargetingStateRecursive(effect.OnSuccess);
            }
            return ActionState.Normal;
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

        public static readonly object SkippedTarget = new object();

        public void TryStartDevourHand(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            // 1. Check for Pre-Selected Target (Pre-Commit Flow)
            var preTarget = GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourHand);
            
            if (preTarget == SkippedTarget)
            {
                _logger.Log("Devour action skipped (Pre-Target).", LogChannel.General);
                // Fix: Do NOT invoke onComplete. 
                // onComplete represents the "OnSuccess" branch (e.g. Supplant).
                // If we skip the Cost (Devour), we do not get the Reward (Supplant).
                return;
            }

            if (preTarget is Card targetCard)
            {
                if (deferExecution)
                {
                    // BUFFER the choice (Pre-Target)
                    PendingDevourCard = targetCard;
                    _logger.Log($"Devour Buffered (Pre-Target): {targetCard.Name}. Proceeding to next step...", LogChannel.Info);
                    
                    // Proceed to next step without executing
                    onComplete?.Invoke();
                    return;
                }
                else
                {
                    // Execute immediately!
                    _matchManager.DevourCard(targetCard);
                    onComplete?.Invoke();
                    return;
                }
            }

            // Dynamic Threshold:
            // If the source card is in Hand, we need at least one OTHER card (Count > 1).
            // If the source card is Played (e.g. during resolution), we just need any card in Hand (Count > 0).
            int requiredCount = (sourceCard.Location == CardLocation.Hand) ? 1 : 0;

            if (CurrentPlayer.Hand.Count <= requiredCount)
            {
                _logger.Log("No other cards in hand to Devour.", LogChannel.Warning);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                // onComplete?.Invoke(); // Do not chain if optional cost not paid
                return;
            }

            StartTargeting(ActionState.TargetingDevourHand, sourceCard);
            _pendingCallback = onComplete;
            _deferDevourExecution = deferExecution; // Store the flag
            _logger.Log($"Select a card from your HAND to Devour (Remove from game). [Defer: {deferExecution}]", LogChannel.General);
        }

        public void HandleDevourSelection(Card? targetCard)
        {
            if (targetCard is null)
            {
                 // Cancelled? This method assumes valid selection from InputMode.
                 return;
            }
            
            if (targetCard == PendingCard) 
            {
                 // Should have been filtered by InputMode, but safety check.
                 _logger.Log("Cannot devour the played card itself.", LogChannel.Warning);
                 return;
            }

            if (_deferDevourExecution)
            {
                // BUFFER the choice
                PendingDevourCard = targetCard;
                _logger.Log($"Devour Buffered: {targetCard.Name}. Proceeding to next step...", LogChannel.Info);
                
                // We do NOT call actionSystem.CompleteAction() yet because we want to maintain the chain?
                // Wait, CompleteAction clears state and calls callback.
                // We DO want to fire callback to start the NEXT effect (Supplant Targeting),
                // but we don't want to clear PendingDevourCard.
                
                // So CompleteAction needs to be careful about what it clears.
                // Actually, CompleteAction clears EVERYTHING.
                // We need to persist PendingDevourCard across CompleteAction if it acts as a transition.
                
                // However, PendingDevourCard belongs to the specific card execution context.
                // If we ClearState(), PendingDevourCard is lost.
                // We must modify ClearState to optionally preserve it, OR
                // we treat this transition differently.
                
                // Implementation Plan said: "Add Card? PendingDevourCard"
                // "Update CancelTargeting to clear pending states."
                
                // If we use CompleteAction(), it invokes _pendingCallback.
                // _pendingCallback starts next StartTargeting(Supplant).
                // If ClearState() wipes PendingDevourCard, we lose it.
                
                // Fix: Modify ClearState to NOT clear PendingDevourCard automatically?
                // Or make PendingDevourCard part of a transient "Transaction Context"?
                // For simplicity: PendingDevourCard is cleared ONLY when we explicitly want to (End of Command).
                
                // Let's modify CompleteAction/ClearState behavior locally or generally.
                // Safest: Don't clear PendingDevourCard in ClearState, but clear it in "FinalizeAction" or manual cleanups.
                // But CancelTargeting MUST clear it.
                
                CompleteAction(); 
                // Note: ClearState() will be called inside CompleteAction. We must update ClearState first.
            }
            else
            {
                // Immediate Execution (Old behavior)
                _matchManager.DevourCard(targetCard);
                CompleteAction();
            }
        }

        private Action? _pendingCallback;

        public void CompleteAction()
        {
            // Fix: Clear state FIRST. 
            // Previous order (Event -> Clear) caused any state set by Event Handlers (e.g. recursive PlayCard) 
            // to be immediately wiped by ClearState.
            ClearState();

            var callback = _pendingCallback;
            _pendingCallback = null; // Clear before invoking to avoid loops

            _logger.Log("ActionSystem: CompleteAction - State cleared. Invoking events/callbacks.", LogChannel.Debug);

            OnActionCompleted?.Invoke(this, EventArgs.Empty);
            callback?.Invoke();
        }
    }
}


