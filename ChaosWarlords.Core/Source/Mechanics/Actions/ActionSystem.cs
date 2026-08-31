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

        /// <summary>
        /// Fired when the logic layer needs a player decision on an optional card effect.
        /// See IActionSystem.OnInteractionRequested - ActionSystem never holds a reference
        /// to IUIEventMediator; the UI layer subscribes to this instead.
        /// </summary>
        public event Action<Core.Contexts.InteractionRequest>? OnInteractionRequested;

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public MapNode? PendingMoveSource { get; private set; }

        // Subsystems
        private readonly DevourSubsystem _devourSubsystem;
        private readonly SpySubsystem _spySubsystem;
        private readonly PreTargetHandler _preTargetHandler;
        private readonly ActionInputController _inputController;

        public ActionSystem(ITurnManager turnManager, IMapManager mapManager, IGameLogger logger)
        {
            _turnManager = turnManager;
            _mapManager = mapManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize Subsystems
            _devourSubsystem = new DevourSubsystem(_turnManager, this, _logger);
            _spySubsystem = new SpySubsystem(_mapManager, _turnManager, this, _logger);
            _preTargetHandler = new PreTargetHandler(_logger, _preSelectedTargets);

            // Click-to-command routing lives in its own class (SRP); ActionSystem stays the
            // logic/state engine and delegates HandleTargetClick to it.
            _inputController = new ActionInputController(this, _mapManager, _spySubsystem, _turnManager, _logger);
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

            // Auto-Execute if Pre-Target exists (Transactional/Replay Flow).
            // onSkipped: currently unreachable specifically for the devour states this
            // targets - TryStartDevourHand/Market/InnerCircle each already intercept and
            // consume a SkippedTarget pre-target themselves (DevourSubsystem's own
            // HandlePreTargetSkipped) before ever calling StartTargeting, so by the time
            // execution reaches here any pre-target for this exact state was already
            // consumed. Handled anyway, matching TryExecutePreTargetEffect's fix - a
            // reachable ExecutionStack must resolve as failure (false), not success, so an
            // OnSuccess child never gets pushed for something that was skipped; falls back
            // to CompleteAction() for the legacy/direct (no stack context) case. See
            // planning.txt.
            if (card != null && _preTargetHandler.TryExecutePreTarget(
                card,
                state,
                HandleTargetClick,
                HandleDevourSelection,
                cmd => OnAutoExecuteCommand?.Invoke(cmd),
                () =>
                {
                    if (ExecutionStack.Count > 0) ResolveCurrentEffect(false);
                    else CompleteAction();
                }))
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
            CancelTargeting();
            OnActionFailed?.Invoke(this, reason);
        }

        public void RaiseActionFailed(string reason)
        {
            _logger.Log($"Action Failed (retryable): {reason}", LogChannel.Warning);
            OnActionFailed?.Invoke(this, reason);
        }

        public void CancelTargeting()
        {
            TryRestoreCardToHand();
            ClearPreselectedTargets();

            var cardToClear = PendingCard;

            ClearState();
            _devourSubsystem.ClearState();
            _logger.Log("ActionSystem: Targeting Cancelled. State cleared.", LogChannel.Info);

            var cancelledEffects = PopCancelledEffects(cardToClear);
            InvokeCancellationCallbacks(cancelledEffects);

            // Resume stack processing ONLY if there are remaining effects
            if (ExecutionStack.Count > 0)
            {
                _logger.Log($"ActionSystem: [CANCEL] Cleanup complete. Resuming stack (Size: {ExecutionStack.Count}).", LogChannel.Debug);
                ProcessStack();
            }
        }

        private void TryRestoreCardToHand()
        {
            if (PendingCard != null && PendingCard.Location == CardLocation.Played)
            {
                CurrentPlayer.RemoveFromPlayed(PendingCard);
                CurrentPlayer.AddToHand(PendingCard);
                PendingCard.Location = CardLocation.Hand;
                _logger.Log($"Returned {PendingCard.Name} to hand after targeting cancellation.", LogChannel.Info);
            }
        }

        private void ClearPreselectedTargets()
        {
            if (PendingCard != null && _preSelectedTargets.ContainsKey(PendingCard))
            {
                _preSelectedTargets.Remove(PendingCard);
                _logger.Log($"Cleared Pre-Targets for {PendingCard.Name} due to Cancellation.", LogChannel.Debug);
            }
        }

        private List<Core.Contexts.EffectContext> PopCancelledEffects(Card? cardToClear)
        {
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

            return cancelledEffects;
        }

        private void InvokeCancellationCallbacks(List<Core.Contexts.EffectContext> cancelledEffects)
        {
            foreach (var effect in cancelledEffects)
            {
                _logger.Log($"ActionSystem: [CANCEL] Popped effect [{effect.EffectType}] for {effect.SourceCard?.Name ?? "Unknown"}.", LogChannel.Debug);
                effect.OnCancelled?.Invoke();
            }
        }

        public bool IsTargeting()
        {
            return CurrentState != ActionState.Normal;
        }

        /// <summary>
        /// Handles a click on a map node/site, delegating to the ActionInputController to
        /// translate it into a command based on the current targeting state.
        /// </summary>
        public IGameCommand? HandleTargetClick(MapNode? targetNode, Site? targetSite)
        {
            return _inputController.HandleTargetClick(targetNode, targetSite);
        }


        // --- Commands Implementation ---

        public void PerformAssassinate(MapNode node, string? cardId, string? devourCardId = null)
        {
            // Transactional Devour Handling (Logic Layer)
            ConsumePendingDevour(devourCardId);

            bool isPaidByCard = !string.IsNullOrEmpty(cardId);

            if (!isPaidByCard)
            {
                SpendAssassinateCost();
            }

            _mapManager.Assassinate(node, CurrentPlayer);
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
                CurrentPlayer.SpendPower(ASSASSINATE_COST);
            }
        }

        public void PerformReturnTroop(MapNode node, string? cardId)
        {
            // CompleteAction(), not a manual OnActionCompleted+ClearState() - see
            // PerformSupplant's comment (same fix, same reasoning: a manual clear never pops
            // ExecutionStack, so it would strand an EffectContext there if this were ever
            // reached via a chained effect). Confirmed via grep this method is currently
            // unreachable from any live path (no command or test calls it - see planning.txt
            // RESOLVED) - fixed anyway so it isn't a landmine for whatever wires it up next,
            // matching this file's PerformAssassinate/PerformSupplant precedent from earlier
            // this session.
            _mapManager.ReturnTroop(node, CurrentPlayer);
            CompleteAction();
        }

        public void PerformSupplant(MapNode node, string? cardId, string? devourCardId = null)
        {
            // Transactional Devour Handling (Logic Layer)
            ConsumePendingDevour(devourCardId);

            _mapManager.Supplant(node, CurrentPlayer);
            // CompleteAction(), not a manual OnActionCompleted+ClearState() - matches
            // PerformAssassinate's pattern. When Supplant was reached via a chained effect
            // (e.g. Wight's Devour -> Supplant), its TargetingSupplant EffectContext is still
            // sitting on ExecutionStack; CompleteAction() pops it via ResolveCurrentEffect,
            // which is what actually fires OnActionCompleted once the stack is genuinely
            // empty (see HandleStackEmptyState). The manual raise+clear this replaced left
            // that effect stuck on the stack forever when Supplant came from a chain - it
            // would resurface and force Supplant targeting on the next unrelated card played
            // (see planning.txt RESOLVED). For a direct, non-chained Supplant (stack already
            // empty), CompleteAction()'s fallback branch does exactly what this used to do.
            CompleteAction();
        }

        /// <summary>
        /// Consumes a devour that was deferred earlier in a chained effect (e.g. Wight's
        /// "Devour a card in your hand -> Supplant a troop" - see CardEffectProcessor.
        /// ApplyDevourWithChain / DevourSubsystem's deferExecution flow). Prefers the
        /// explicit devourCardId (authoritative, carried on the command DTO for replay);
        /// falls back to PendingDevourCard for the pre-target/replay flow, which doesn't
        /// thread an id through PerformSupplant's caller.
        ///
        /// Always clears the devour subsystem's pending state afterward, whether or not
        /// there was anything to consume - PendingDevourCard is deliberately NOT cleared
        /// by ClearState()/CompleteAction() (see the comment on ClearState below) so it
        /// can survive across the chained targeting steps, but that means it would
        /// otherwise leak into the next unrelated Assassinate/Supplant the player makes
        /// this turn (ActionInputController reads PendingDevourCard unconditionally), and
        /// MatchManager.FindCardInPlayerCollections falls back to matching by Card.Id, so
        /// a stale reference could wrongly devour an unrelated duplicate-copy card later.
        /// </summary>
        private void ConsumePendingDevour(string? devourCardId)
        {
            if (!string.IsNullOrEmpty(devourCardId))
            {
                var cardToDevour = CurrentPlayer.Hand.FirstOrDefault(c => c.Id == devourCardId);
                if (cardToDevour != null) _matchManager.DevourCard(cardToDevour);
                _devourSubsystem.ClearState();
            }
            else if (PendingDevourCard != null)
            {
                _matchManager.DevourCard(PendingDevourCard);
                _devourSubsystem.ClearState();
            }
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

        /// <summary>
        /// Sets the source node for a Move Troop sequence and transitions to destination-targeting.
        /// Called by ActionInputController once it has validated the source node.
        /// </summary>
        public void SetMoveSource(MapNode? node)
        {
            PendingMoveSource = node;
            CurrentState = ActionState.TargetingMoveDestination;
            _logger.Log("Select an empty destination space anywhere on the board.", LogChannel.General);
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

        // --- Extracted Helper Methods for ProcessStack() ---

        /// <summary>
        /// Processes an optional effect by performing deep lookahead validation and raising
        /// OnInteractionRequested for the UI layer to present a confirmation prompt.
        /// </summary>
        /// <returns>True if the effect was handled (interaction request raised or effect skipped), false otherwise</returns>
        private bool ProcessOptionalEffect(Core.Contexts.EffectContext effect)
        {
            if (OnInteractionRequested == null) return false;

            // Deep Lookahead: Check if OnSuccess chain has valid targets
            if (effect.SourceEffect?.OnSuccess != null && _matchContext != null)
            {
                var onSuccessEffect = effect.SourceEffect.OnSuccess;
                bool onSuccessRequiresTargeting = _matchContext.CardRuleEngine.GetStrategy(onSuccessEffect.Type).IsTargetingEffect;

                if (onSuccessRequiresTargeting)
                {
                    bool hasValidTargets = _matchContext.CardRuleEngine.HasValidTargets(
                        _matchContext.ActivePlayer,
                        onSuccessEffect.Type,
                        effect.SourceCard
                    );

                    if (!hasValidTargets)
                    {
                        _logger.Log($"ActionSystem: Skipping optional effect {effect.SourceEffect.Type} - OnSuccess effect {onSuccessEffect.Type} has no valid targets.", LogChannel.Warning);
                        ResolveCurrentEffect(false);
                        return true;
                    }
                }
            }

            _logger.Log($"ActionSystem: Requesting optional effect confirmation for {effect.SourceEffect?.Type}...", LogChannel.Input);

            var request = new Core.Contexts.InteractionRequest(effect, accepted =>
            {
                if (accepted) HandleOptionalEffectAccepted(effect);
                else HandleOptionalEffectDeclined(effect);
            });

            OnInteractionRequested.Invoke(request);

            return true;
        }

        /// <summary>
        /// Handles user acceptance of an optional effect.
        /// </summary>
        private void HandleOptionalEffectAccepted(Core.Contexts.EffectContext effect)
        {
            _logger.Log($"ActionSystem: Optional effect {effect.SourceEffect?.Type} accepted.", LogChannel.Input);

            // User accepted - NOW we set the state to Targeting (if applicable)
            if (CurrentState == ActionState.Normal && effect.EffectType != ActionState.Normal)
            {
                CurrentState = effect.EffectType;
                _logger.Log($"ActionSystem: [PROCESS] Optional Accepted -> State set to: {CurrentState}", LogChannel.Debug);
            }

            // User accepted - execute the effect
            if (effect.SourceEffect?.Type == EffectType.Devour)
            {
                var strategy = Mechanics.Rules.DevourStrategyFactory.GetStrategy(effect.SourceEffect.TargetLocation);
                strategy.Execute(effect.SourceCard, _matchContext!, _logger, () => ResolveCurrentEffect(true), false);
            }
            // For other optional effects, continue to normal targeting flow
        }

        /// <summary>
        /// Handles user declining of an optional effect.
        /// </summary>
        private void HandleOptionalEffectDeclined(Core.Contexts.EffectContext effect)
        {
            _logger.Log($"ActionSystem: Optional effect {effect.SourceEffect?.Type} declined.", LogChannel.Input);
            ResolveCurrentEffect(false);
        }

        /// <summary>
        /// Attempts to execute a pre-selected target for an effect (used in replay/testing).
        /// </summary>
        /// <returns>True if pre-target was found and executed</returns>
        private bool TryExecutePreTargetEffect(Core.Contexts.EffectContext effect)
        {
            if (effect.SourceCard == null) return false;

            bool executed = _preTargetHandler.TryExecutePreTarget(
                effect.SourceCard,
                effect.EffectType,
                HandleTargetClick,
                HandleDevourSelection,
                cmd => OnAutoExecuteCommand?.Invoke(cmd),
                () => ResolveCurrentEffect(false)
            );

            if (executed)
            {
                _logger.Log($"ActionSystem: Pre-Target executed for {effect.EffectType}. Continuing stack...", LogChannel.Debug);
            }

            return executed;
        }

        /// <summary>
        /// Processes an automatic (non-targeting) effect by applying it immediately.
        /// </summary>
        private void ProcessAutomaticEffect(Core.Contexts.EffectContext effect)
        {
            // Automatic Effect (e.g. GainResource, DrawCard)
            if (effect.SourceEffect != null && _matchContext != null)
            {
                Mechanics.Rules.CardEffectProcessor.ApplyEffect(effect.SourceEffect, effect.SourceCard, _matchContext, _logger);
            }

            ResolveCurrentEffect(true);
        }

        /// <summary>
        /// Sets up the action state for a required (non-optional) targeting effect.
        /// </summary>
        private void SetupTargetingForRequiredEffect(Core.Contexts.EffectContext effect)
        {
            _logger.Log($"ActionSystem: [PROCESS] Effect requires input. Setting state to [{effect.EffectType}]", LogChannel.Debug);
            CurrentState = effect.EffectType;
            _logger.Log($"ActionSystem: [PROCESS] State set to: {CurrentState}", LogChannel.Debug);
        }

        public void ProcessStack()
        {
            _logger.Log($"ActionSystem: [PROCESS] ProcessStack called. Stack size: {ExecutionStack.Count}, Current state: {CurrentState}", LogChannel.Debug);

            if (HandleStackEmptyState())
            {
                return;
            }

            var nextEffect = ExecutionStack.Peek();
            _logger.Log($"ActionSystem: [PROCESS] Next effect: [{nextEffect.EffectType}] RequiresInput={nextEffect.RequiresInput}, SourceEffect={nextEffect.SourceEffect?.Type}", LogChannel.Debug);

            if (!nextEffect.RequiresInput)
            {
                ProcessAutomaticEffect(nextEffect);
                return;
            }

            HandleInputRequiredEffect(nextEffect);
        }

        private bool HandleStackEmptyState()
        {
            if (ExecutionStack.Count == 0)
            {
                _logger.Log("ActionSystem: [PROCESS] Stack Empty. Sequence Complete.", LogChannel.Debug);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                ClearState();
                return true;
            }
            return false;
        }

        private void HandleInputRequiredEffect(Core.Contexts.EffectContext nextEffect)
        {
            // Effect requires user input
            PendingCard = nextEffect.SourceCard;
            bool isOptional = nextEffect.SourceEffect?.IsOptional == true;

            if (!isOptional)
            {
                SetupTargetingForRequiredEffect(nextEffect);
            }

            // Handle optional effects with UI confirmation
            if (isOptional && ProcessOptionalEffect(nextEffect))
            {
                return; // Wait for user choice
            }

            // Try to execute pre-selected targets (for replay/testing)
            if (TryExecutePreTargetEffect(nextEffect))
            {
                return; // Pre-target was executed
            }

            _logger.Log($"ActionSystem: Waiting for input for {nextEffect.EffectType}...", LogChannel.Input);
        }
    }
}


