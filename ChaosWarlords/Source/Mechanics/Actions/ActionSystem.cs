using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
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
        private IUIEventMediator? _uiMediator;

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public MapNode? PendingMoveSource { get; private set; }

        // Subsystems
        private readonly DevourSubsystem _devourSubsystem;
        private readonly SpySubsystem _spySubsystem;
        private readonly PreTargetHandler _preTargetHandler;

        public ActionSystem(ITurnManager turnManager, IMapManager mapManager, IGameLogger logger)
        {
            _turnManager = turnManager;
            _mapManager = mapManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize Subsystems
            _devourSubsystem = new DevourSubsystem(_turnManager, this, _logger);
            _spySubsystem = new SpySubsystem(_mapManager, _turnManager, this, _logger);
            _preTargetHandler = new PreTargetHandler(_logger, _preSelectedTargets);

            _actionHandlers = new Dictionary<ActionState, Func<MapNode?, Site?, IGameCommand?>>();
            InitializeHandlers();
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
            _devourSubsystem.SetPlayerStateManager(stateManager);
            // SpySubsystem uses PlayerStateManager via ActionSystem? No, it has its own logic that might need it?
            // Checking SpySubsystem: It has SetPlayerStateManager.
            if (_spySubsystem is SpySubsystem concreteSpy)
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

        public void SetUIMediator(IUIEventMediator uiMediator)
        {
            _uiMediator = uiMediator;
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

            // BUG FIX: Validate that valid targets exist before starting targeting
            if (!_mapManager.HasValidReturnSpyTarget(CurrentPlayer))
            {
                OnActionFailed?.Invoke(this, "No enemy spies to return!");
                _logger.Log("Return Spy failed: No valid targets.", LogChannel.Warning);
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
            if (card != null && _preTargetHandler.TryExecutePreTarget(
                card,
                state,
                HandleTargetClick,
                HandleDevourSelection,
                cmd => OnAutoExecuteCommand?.Invoke(cmd)))
            {
                // Pre-target was found and executed
                return;
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
            // BUG FIX: Return card to hand if targeting is cancelled
            // This provides a "safety net" for misclicks or strategy changes
            if (PendingCard != null && PendingCard.Location == CardLocation.Played)
            {
                CurrentPlayer.RemoveFromPlayed(PendingCard);
                CurrentPlayer.AddToHand(PendingCard);
                PendingCard.Location = CardLocation.Hand;
                _logger.Log($"Returned {PendingCard.Name} to hand after targeting cancellation.", LogChannel.Info);
            }

            // Clear Pre-Selected targets to prevent "Zombie" executions if we restart
            if (PendingCard != null && _preSelectedTargets.ContainsKey(PendingCard))
            {
                _preSelectedTargets.Remove(PendingCard);
                _logger.Log($"Cleared Pre-Targets for {PendingCard.Name} due to Cancellation.", LogChannel.Debug);
            }

            // NEW: Resolve ALL effects associated with the cancelled card to prevent "Zombie" executions.
            // We manualy Pop to avoid ResolveCurrentEffect triggering ProcessStack recursively for each item.
            var cardToClear = PendingCard;

            ClearState();
            _devourSubsystem.ClearState();
            _logger.Log("ActionSystem: Targeting Cancelled. State cleared.", LogChannel.Info);

            var cancelledEffects = new List<Core.Contexts.EffectContext>();

            if (ExecutionStack.Count > 0)
            {
                // Always pop the top effect (current targeting effect being cancelled)
                cancelledEffects.Add(ExecutionStack.Pop());

                // Continue popping if subsequent effects belong to the same card
                if (cardToClear != null)
                {
                    while (ExecutionStack.Count > 0 && ExecutionStack.Peek().SourceCard == cardToClear)
                    {
                        cancelledEffects.Add(ExecutionStack.Pop());
                    }
                }
            }

            // Invoke Cancellation Callbacks (if any)
            foreach (var effect in cancelledEffects)
            {
                _logger.Log($"ActionSystem: [CANCEL] Popped effect [{effect.EffectType}] for {effect.SourceCard?.Name ?? "Unknown"}.", LogChannel.Debug);
                effect.OnCancelled?.Invoke();
            }

            // Finally, resume stack processing ONLY if there are remaining effects (from previous cards).
            // If stack is empty, we are done with cancellation and should NOT trigger OnActionCompleted.
            if (ExecutionStack.Count > 0)
            {
                _logger.Log($"ActionSystem: [CANCEL] Cleanup complete. Resuming stack (Size: {ExecutionStack.Count}).", LogChannel.Debug);
                ProcessStack();
            }
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

            return new AssassinateCommand(targetNode.Id, PendingCard?.Id, PendingDevourCard?.Id);
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
                CurrentPlayer.SpendPower(ASSASSINATE_COST);
            }
        }

        private ReturnTroopCommand? HandleReturn(MapNode targetNode)
        {
            if (targetNode is null) return null;
            if (targetNode.Occupant != PlayerColor.None && _mapManager.HasPresence(targetNode, CurrentPlayer.Color))
            {
                if (targetNode.Occupant == PlayerColor.Neutral) return null;

                return new ReturnTroopCommand(targetNode.Id, PendingCard?.Id);
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

            return new SupplantCommand(targetNode.Id, PendingCard?.Id, PendingDevourCard?.Id);
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

            // Try to execute pre-target if it exists
            if (TryExecuteSupplantPreTarget(preTarget, sourceCard))
            {
                return;
            }

            // Normal Flow - validate and start targeting
            if (!CanStartSupplant(sourceCard, out string? failureReason))
            {
                if (failureReason != null)
                {
                    _logger.Log($"{sourceCard.Name}: {failureReason}", LogChannel.Warning);
                }
                return;
            }

            StartTargeting(ActionState.TargetingSupplant, sourceCard);
            _logger.Log($"{sourceCard.Name}: Initiating Supplant targeting. Select a valid target.", LogChannel.Input);
        }

        private bool TryExecuteSupplantPreTarget(object? preTarget, Card sourceCard)
        {
            if (preTarget == null) return false;

            MapNode? targetNode = preTarget switch
            {
                int nodeId => _mapManager.Nodes.FirstOrDefault(n => n.Id == nodeId),
                MapNode node => node,
                _ => null
            };

            if (targetNode != null)
            {
                _logger.Log($"Supplant Pre-Target found: Node {targetNode.Id}. Executing...", LogChannel.Info);
                PerformSupplant(targetNode, sourceCard.Id);
                return true;
            }

            return false;
        }

        private bool CanStartSupplant(Card sourceCard, out string? failureReason)
        {
            bool canAssassinate = _mapManager.HasValidAssassinationTarget(CurrentPlayer);
            bool hasTroops = CurrentPlayer.TroopsInBarracks > 0;

            if (!hasTroops)
            {
                failureReason = "Cannot Supplant (No Troops in Barracks).";
                return false;
            }

            if (!canAssassinate)
            {
                failureReason = "No valid targets to Supplant.";
                return false;
            }

            failureReason = null;
            return true;
        }

        public bool AdvancePreCommitTargeting(Card sourceCard)
        {
            // Determine if the *current* step was skipped by the user, so the Engine knows to skip its children.
            bool isCurrentSkipped = IsPreTargetSkipped(sourceCard, CurrentState);

            var nextState = Mechanics.Rules.TargetingStateEngine.DetermineNextState(sourceCard.Effects, CurrentState, isCurrentSkipped, _matchContext!.CardRuleEngine);

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

            return new MoveTroopCommand(PendingMoveSource.Id, targetNode.Id, PendingCard?.Id);
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

        public void TryStartDevourInnerCircle(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            _devourSubsystem.TryStartDevourInnerCircle(sourceCard, onComplete, deferExecution);
        }

        public void DeferDevour(Card card)
        {
            _devourSubsystem.DeferDevour(card);
        }



        public DevourCardCommand? HandleDevourMarketSelection(Card? targetCard)
        {
            return _devourSubsystem.HandleDevourMarketSelection(targetCard);
        }

        public DevourCardCommand? HandleDevourInnerCircleSelection(Card? targetCard)
        {
            return _devourSubsystem.HandleDevourInnerCircleSelection(targetCard);
        }

        public DevourCardCommand? HandleDevourSelection(Card? targetCard)
        {
            return _devourSubsystem.HandleDevourSelection(targetCard);
        }
        public void CompleteAction()
        {
            // NEW STACK LOGIC:
            // Completing an action (like Assassinate or Return Spy) implies the current "Blocking" effect on the stack is resolved.
            // We resolve it with Success=true.

            if (ExecutionStack.Count > 0)
            {
                _logger.Log("ActionSystem: CompleteAction invoked. Resolving current stack effect...", LogChannel.Debug);
                ResolveCurrentEffect(true);
            }
            else
            {
                // Fallback: Legacy/Direct mode support (Unit Tests or Actions without Effects)
                _logger.Log("ActionSystem: CompleteAction (Direct). No stack context.", LogChannel.Debug);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                ClearState();
            }
        }

        // --- Stack-Based Architecture ---

        public Stack<Core.Contexts.EffectContext> ExecutionStack { get; } = new();

        public Core.Contexts.EffectContext? CurrentEffect => ExecutionStack.Count > 0 ? ExecutionStack.Peek() : null;

        public void PushEffect(Core.Contexts.EffectContext context)
        {
            ExecutionStack.Push(context);
            _logger.Log($"ActionSystem: Pushed Effect [{context.EffectType}] from {context.SourceCard?.Name}. Stack Size: {ExecutionStack.Count}", LogChannel.Debug);
        }

        public void ResolveCurrentEffect(bool success)
        {
            if (ExecutionStack.Count == 0)
            {
                _logger.Log("ActionSystem: ResolveCurrentEffect called but stack is empty!", LogChannel.Warning);
                return;
            }

            var effect = ExecutionStack.Pop();
            _logger.Log($"ActionSystem: [RESOLVE] Popped effect [{effect.EffectType}] Success={success}. Remaining Stack: {ExecutionStack.Count}", LogChannel.Debug);
            _logger.Log($"ActionSystem: [RESOLVE] Effect has callback: {effect.OnResolved != null}, SourceEffect: {effect.SourceEffect?.Type}", LogChannel.Debug);

            if (success)
            {
                if (effect.OnResolved != null)
                {
                    _logger.Log($"ActionSystem: [RESOLVE] Invoking OnResolved callback for [{effect.EffectType}]...", LogChannel.Debug);
                    effect.OnResolved.Invoke(true);
                    _logger.Log($"ActionSystem: [RESOLVE] OnResolved callback completed. Stack now has {ExecutionStack.Count} items.", LogChannel.Debug);
                }
                else
                {
                    _logger.Log($"ActionSystem: [RESOLVE] No OnResolved callback for [{effect.EffectType}]", LogChannel.Debug);
                }
            }
            else
            {
                effect.OnCancelled?.Invoke();
            }

            // After popping, process the next item
            _logger.Log($"ActionSystem: [RESOLVE] Calling ProcessStack to continue. Stack size: {ExecutionStack.Count}", LogChannel.Debug);
            ProcessStack();
            _logger.Log($"ActionSystem: [RESOLVE] ProcessStack returned. Current state: {CurrentState}", LogChannel.Debug);
        }

        private Contexts.MatchContext? _matchContext;
        public void SetMatchContext(Contexts.MatchContext context)
        {
            _matchContext = context;
        }

        public void ProcessStack()
        {
            _logger.Log($"ActionSystem: [PROCESS] ProcessStack called. Stack size: {ExecutionStack.Count}, Current state: {CurrentState}", LogChannel.Debug);

            if (ExecutionStack.Count == 0)
            {
                // Sequence Complete
                _logger.Log("ActionSystem: [PROCESS] Stack Empty. Sequence Complete.", LogChannel.Debug);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                ClearState();
                return;
            }

            var nextEffect = ExecutionStack.Peek();
            _logger.Log($"ActionSystem: [PROCESS] Next effect: [{nextEffect.EffectType}] RequiresInput={nextEffect.RequiresInput}, SourceEffect={nextEffect.SourceEffect?.Type}", LogChannel.Debug);

            if (nextEffect.RequiresInput)
            {
                // Set PendingCard but DEFER State Change if Optional
                PendingCard = nextEffect.SourceCard;
                bool isOptional = nextEffect.SourceEffect?.IsOptional == true;

                if (!isOptional)
                {
                    _logger.Log($"ActionSystem: [PROCESS] Effect requires input. Setting state to [{nextEffect.EffectType}]", LogChannel.Debug);
                    CurrentState = nextEffect.EffectType;
                    _logger.Log($"ActionSystem: [PROCESS] State set to: {CurrentState}", LogChannel.Debug);
                }

                // Handle Optional Effects - Show UI Popup
                if (isOptional && _uiMediator != null)
                {
                    // Deep Lookahead: Check if OnSuccess chain has valid targets
                    // If the OnSuccess effect requires targeting and has no valid targets, skip the popup
                    if (nextEffect.SourceEffect?.OnSuccess != null && _matchContext != null)
                    {
                        var onSuccessEffect = nextEffect.SourceEffect.OnSuccess;
                        
                        // Use Strategy Pattern via RuleEngine
                        bool onSuccessRequiresTargeting = _matchContext.CardRuleEngine.GetStrategy(onSuccessEffect.Type).IsTargetingEffect;

                        if (onSuccessRequiresTargeting)
                        {
                            bool hasValidTargets = _matchContext.CardRuleEngine.HasValidTargets(
                                _matchContext.ActivePlayer,
                                onSuccessEffect.Type,
                                nextEffect.SourceCard
                            );

                            if (!hasValidTargets)
                            {
                                _logger.Log($"ActionSystem: Skipping optional effect {nextEffect.SourceEffect.Type} - OnSuccess effect {onSuccessEffect.Type} has no valid targets.", LogChannel.Warning);
                                // Skip this effect and move to next
                                ResolveCurrentEffect(false);
                                return;
                            }
                        }
                    }

                    _logger.Log($"ActionSystem: Requesting optional effect confirmation for {nextEffect.SourceEffect?.Type}...", LogChannel.Input);

                    _uiMediator.RequestOptionalEffect(
                        nextEffect.SourceCard,
                        nextEffect.SourceEffect!,
                        onAccept: () =>
                        {
                            _logger.Log($"ActionSystem: Optional effect {nextEffect.SourceEffect?.Type} accepted.", LogChannel.Input);

                            // User accepted - NOW we set the state to Targeting (if applicable)
                            if (CurrentState == ActionState.Normal && nextEffect.EffectType != ActionState.Normal)
                            {
                                CurrentState = nextEffect.EffectType;
                                _logger.Log($"ActionSystem: [PROCESS] Optional Accepted -> State set to: {CurrentState}", LogChannel.Debug);
                            }

                            // User accepted - execute the effect
                            // For Devour Self, execute the strategy directly
                            if (nextEffect.SourceEffect?.Type == EffectType.Devour && nextEffect.SourceEffect.TargetLocation == CardLocation.Self)
                            {
                                var strategy = Mechanics.Rules.DevourStrategyFactory.GetStrategy(CardLocation.Self);
                                strategy.Execute(nextEffect.SourceCard, _matchContext!, _logger, () =>
                                {
                                    // OnComplete callback - resolve the effect
                                    ResolveCurrentEffect(true);
                                }, false);
                            }
                            // For other optional Devour effects (Market, Hand, InnerCircle), call the strategy
                            else if (nextEffect.SourceEffect?.Type == EffectType.Devour)
                            {
                                var strategy = Mechanics.Rules.DevourStrategyFactory.GetStrategy(nextEffect.SourceEffect.TargetLocation);
                                strategy.Execute(nextEffect.SourceCard, _matchContext!, _logger, () =>
                                {
                                    // OnComplete callback - resolve the effect
                                    ResolveCurrentEffect(true);
                                }, false);
                            }
                            else
                            {
                                // For other optional effects, continue to normal targeting flow
                                // Don't return - let it fall through
                            }
                        },
                        onDecline: () =>
                        {
                            _logger.Log($"ActionSystem: Optional effect {nextEffect.SourceEffect?.Type} declined.", LogChannel.Input);
                            // Skip this effect and move to next
                            ResolveCurrentEffect(false);
                        }
                    );

                    return; // Wait for user choice (callbacks will handle continuation)
                }

                // Auto-Execute if Pre-Target exists (Test/Replay support)
                if (nextEffect.SourceCard != null && _preTargetHandler.TryExecutePreTarget(
                    nextEffect.SourceCard,
                    nextEffect.EffectType,
                    HandleTargetClick,
                    HandleDevourSelection,
                    cmd => OnAutoExecuteCommand?.Invoke(cmd)))
                {
                    _logger.Log($"ActionSystem: Pre-Target executed for {nextEffect.EffectType}. Continuing stack...", LogChannel.Debug);
                    // Don't return - the pre-target execution may have resolved the effect?
                    // TryExecutePreTarget returns true if executed. 
                    // If executed, we should verify if stack advanced?
                    // Usually pre-target executes command -> Resolve -> Pop.
                    // So we should return?
                    return;
                }

                _logger.Log($"ActionSystem: Waiting for input for {nextEffect.EffectType}...", LogChannel.Input);
            }
            else
            {
                // Automatic Effect (e.g. GainResource, DrawCard)
                // Execute immediately via CardEffectProcessor logic
                if (nextEffect.SourceEffect != null && _matchContext != null)
                {
                    Mechanics.Rules.CardEffectProcessor.ApplyEffect(nextEffect.SourceEffect, nextEffect.SourceCard, _matchContext, _logger);
                }

                ResolveCurrentEffect(true);
            }
        }
    }
}


