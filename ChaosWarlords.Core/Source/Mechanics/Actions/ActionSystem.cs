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
        private readonly ActionExecutionEngine _executionEngine;

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

            // Execution-stack management (PushEffect/ResolveCurrentEffect/ProcessStack and
            // everything ProcessStack calls into) lives in its own class too, extracted
            // 2026-08-31 - see ActionExecutionEngine's doc comment and planning.txt. Forward
            // its three events as ActionSystem's own public events (C# events can only be
            // raised by their declaring type, so this can't just be "the engine invokes
            // ActionSystem's event directly" even through the shared IActionSystem reference).
            _executionEngine = new ActionExecutionEngine(this, _logger, _preTargetHandler);
            _executionEngine.OnActionCompleted += (sender, args) => OnActionCompleted?.Invoke(this, args);
            _executionEngine.OnInteractionRequested += request => OnInteractionRequested?.Invoke(request);
            _executionEngine.OnAutoExecuteCommand += command => OnAutoExecuteCommand?.Invoke(command);
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

        /// <summary>
        /// Full-state snapshot taken exactly once per targeting SEQUENCE (not once per step),
        /// so CancelTargeting() can revert whatever mutated during the sequence instead of
        /// hand-coding a per-mechanic undo step (the "imperative undo trap" - see
        /// CancelTargeting's own doc comment and planning.txt). Captured only when actually
        /// leaving Normal state: AdvancePreCommitTargeting calls StartTargeting repeatedly for
        /// a single multi-step card (e.g. Wight's Devour -> Supplant chain), and re-snapshotting
        /// at every step would only let a later cancel undo back to the START OF THAT STEP, not
        /// the whole play attempt - cancelling ANY step of a chain has always meant "undo the
        /// whole thing" (see TryRestoreCardToHand, unaffected by which step triggered the
        /// cancel). Null whenever there's no MatchContext wired (SetMatchContext never called)
        /// or no sequence in flight - CancelTargeting falls back to the original field-by-field
        /// clear in that case.
        /// </summary>
        private Core.Data.Dtos.GameStateDto? _targetingSequenceSnapshot;

        /// <summary>
        /// Best-effort, matching CommandDispatcher.TryCreateSnapshot's exact precedent for the
        /// same underlying operation (DtoMapper.ToGameStateDto): a lightly-mocked test double
        /// (e.g. an IMarketManager substitute with MarketRow left unconfigured, defaulting to
        /// null) can make the full state-graph traversal throw. Falling back to "no snapshot"
        /// (CancelTargeting then uses its own field-by-field fallback) is strictly better than
        /// letting an ordinary StartTargeting call crash the whole test/game over a snapshot
        /// this specific cancel might not even need.
        /// </summary>
        private Core.Data.Dtos.GameStateDto? TryCreateTargetingSnapshot()
        {
            try
            {
                return Core.Utilities.DtoMapper.ToGameStateDto(_matchContext!);
            }
            catch (Exception ex)
            {
                _logger.Log($"ActionSystem: Could not snapshot state for targeting cancellation ({ex.Message}). CancelTargeting will fall back to field-by-field clearing.", LogChannel.Warning);
                return null;
            }
        }

        public void StartTargeting(ActionState state, Card? card = null)
        {
            if (CurrentState == ActionState.Normal && _matchContext != null)
            {
                _targetingSequenceSnapshot = TryCreateTargetingSnapshot();
            }

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
                    if (_executionEngine.ExecutionStack.Count > 0) _executionEngine.ResolveCurrentEffect(false);
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

            // Whenever we return to Normal - whether the sequence completed successfully or
            // was cancelled via the fallback (no-snapshot) path - any snapshot taken for it is
            // stale. CancelTargeting's own snapshot-restore path already nulls this out
            // itself; this covers every other path back to Normal so a later cancel never
            // reuses a snapshot from a sequence that already finished.
            _targetingSequenceSnapshot = null;
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

        /// <summary>
        /// Cancels the current targeting sequence and reverts to Normal. Two mechanisms work
        /// together here, covering different parts of the timeline:
        ///
        /// 1. A full-state snapshot/restore (see _targetingSequenceSnapshot), taken at the
        ///    moment the sequence actually started. This is the "real fix" planning.txt asked
        ///    for: instead of every mechanic that mutates state during targeting needing its
        ///    own bespoke undo step, ANY such mutation (map, player resources, market, void,
        ///    the effect stack, ActionSystem's own Pending*/CurrentState) reverts automatically.
        ///
        /// 2. TryRestoreCardToHand, kept from before this refactor. The snapshot above CANNOT
        ///    undo the played-card's move from Hand to Played: MatchManager.PlayCard moves a
        ///    card to Played and pays its cost BEFORE pushing its effects onto the stack (see
        ///    MatchManager.PlayCard), so even a snapshot taken at the true start of THIS
        ///    targeting sequence is already post-play. This runs by card ID, not by object
        ///    reference, and AFTER the snapshot restore (not before) - the restore replaces
        ///    the player's Hand/Played collections wholesale with freshly-resolved Card
        ///    instances (see StateRestorer's own doc comments on Card identity across a
        ///    restore), so the pre-cancel PendingCard reference may no longer be the same
        ///    object the restored PlayedCards collection holds; running before the restore
        ///    would just have its own fix immediately overwritten by it.
        /// </summary>
        public void CancelTargeting()
        {
            string? cardToClearId = PendingCard?.Id;

            ClearPreselectedTargets();

            var cancelledEffects = PopCancelledEffects(PendingCard);
            InvokeCancellationCallbacks(cancelledEffects);

            if (_matchContext != null && _targetingSequenceSnapshot != null)
            {
                Managers.StateRestorer.RestoreState(_matchContext, _targetingSequenceSnapshot);
                _targetingSequenceSnapshot = null;
                _logger.Log("ActionSystem: Targeting Cancelled. Full pre-sequence state restored.", LogChannel.Info);
            }
            else
            {
                ClearState();
                _devourSubsystem.ClearState();
                _logger.Log("ActionSystem: Targeting Cancelled (no snapshot available - state cleared directly).", LogChannel.Info);
            }

            TryRestoreCardToHand(cardToClearId);

            // Resume stack processing ONLY if there are remaining effects
            if (_executionEngine.ExecutionStack.Count > 0)
            {
                _logger.Log($"ActionSystem: [CANCEL] Cleanup complete. Resuming stack (Size: {_executionEngine.ExecutionStack.Count}).", LogChannel.Debug);
                _executionEngine.ProcessStack();
            }
        }

        private void TryRestoreCardToHand(string? cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;

            var card = CurrentPlayer.PlayedCards.FirstOrDefault(c => c.Id == cardId);
            if (card != null)
            {
                CurrentPlayer.RemoveFromPlayed(card);
                CurrentPlayer.AddToHand(card);
                card.Location = CardLocation.Hand;
                _logger.Log($"Returned {card.Name} to hand after targeting cancellation.", LogChannel.Info);
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
            var executionStack = _executionEngine.ExecutionStack;

            if (executionStack.Count > 0)
            {
                // Always pop the top effect (current targeting effect being cancelled)
                cancelledEffects.Add(executionStack.Pop());

                // Continue popping if subsequent effects belong to the same card
                if (cardToClear != null)
                {
                    while (executionStack.Count > 0 && executionStack.Peek().SourceCard == cardToClear)
                    {
                        cancelledEffects.Add(executionStack.Pop());
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
            // Empty barracks doesn't block Supplant - the deploy half grants 1 VP instead
            // (rulebook p.12/22, same as the plain Deploy action). Only the assassinate
            // half's target requirement gates this.
            bool canAssassinate = _mapManager.HasValidAssassinationTarget(CurrentPlayer);

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

        public void SetPendingSiteForChain(Site site)
        {
            PendingSite = site;
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

        public void TryStartPlayFromMarket(Card sourceCard, int maxCost)
        {
            if (_marketManager == null || !_marketManager.MarketRow.Any(c => c.Cost <= maxCost))
            {
                _logger.Log($"{sourceCard.Name}: No market card costing {maxCost} or less to play.", LogChannel.Warning);
                CompleteAction();
                return;
            }

            // Reuses IMarketStateManager.OpenForDevour - same "pick a market card, get a
            // command back from a callback" shape Devour-from-Market already uses; only the
            // command the callback builds differs.
            _marketStateManager?.OpenForDevour((card) => HandlePlayFromMarketSelection(card, sourceCard, maxCost));
            StartTargeting(ActionState.TargetingPlayFromMarket, sourceCard);
            _logger.Log($"{sourceCard.Name}: Select a card from the Market (cost <= {maxCost}) to play.", LogChannel.General);
        }

        private Commands.PlayFromMarketCommand? HandlePlayFromMarketSelection(Card? targetCard, Card sourceCard, int maxCost)
        {
            _marketStateManager?.Close();

            if (targetCard is null || targetCard.Location != CardLocation.Market)
            {
                NotifyFailure("Selected card is not in the Market.");
                return null;
            }

            if (targetCard.Cost > maxCost)
            {
                // Server-side re-check, matching every other command's don't-trust-the-client
                // pattern - the client's market UI is expected to have already filtered this.
                NotifyFailure($"That card costs more than {maxCost}.");
                return null;
            }

            return new Commands.PlayFromMarketCommand(targetCard, sourceCard);
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

            if (_executionEngine.ExecutionStack.Count > 0)
            {
                _logger.Log("ActionSystem: CompleteAction invoked. Resolving current stack effect...", LogChannel.Debug);
                _executionEngine.ResolveCurrentEffect(true);
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
        // Delegates straight through to ActionExecutionEngine - see its doc comment and
        // planning.txt for why this moved out of ActionSystem itself. Kept here, not removed
        // from IActionSystem, so every existing caller (input modes, commands, tests) is
        // completely unaffected by the extraction.

        public Stack<Core.Contexts.EffectContext> ExecutionStack => _executionEngine.ExecutionStack;

        public Core.Contexts.EffectContext? CurrentEffect => _executionEngine.CurrentEffect;

        public void PushEffect(Core.Contexts.EffectContext context) => _executionEngine.PushEffect(context);

        public void ResolveCurrentEffect(bool success) => _executionEngine.ResolveCurrentEffect(success);

        public void ProcessStack() => _executionEngine.ProcessStack();

        private Contexts.MatchContext? _matchContext;
        public void SetMatchContext(Contexts.MatchContext context)
        {
            _matchContext = context;
            _executionEngine.SetMatchContext(context);
        }

        /// <summary>
        /// See IActionSystem.RestorePendingState - restore-only, StateRestorer's exclusive
        /// caller. Bypasses CurrentState's normal OnStateChanged-raising setter path
        /// deliberately: a rollback isn't a real state transition any UI/subscriber should
        /// react to, it's undoing one that (from their perspective) never should have
        /// happened. Setting the backing field directly, not the property, is what skips that.
        ///
        /// Also invalidates _targetingSequenceSnapshot unconditionally: this method means an
        /// external authority (StateRestorer, via CancelTargeting's own restore OR
        /// CommandDispatcher's separate rollback-on-exception path) just overwrote CurrentState/
        /// Pending* directly, bypassing ClearState() entirely - so any locally-cached snapshot
        /// for tracking "the start of the sequence that WAS in progress" is stale regardless of
        /// which path got here or what state is being restored to.
        /// </summary>
        public void RestorePendingState(ActionState state, Card? pendingCard, Site? pendingSite, MapNode? pendingMoveSource, Card? pendingDevourCard)
        {
            _currentState = state;
            PendingCard = pendingCard;
            PendingSite = pendingSite;
            PendingMoveSource = pendingMoveSource;
            _devourSubsystem.RestorePendingDevourCard(pendingDevourCard);
            _targetingSequenceSnapshot = null;
        }

        // --- IActionSystem "engine-only" methods - ActionExecutionEngine's exclusive
        // callers. See their doc comments on IActionSystem. ---

        public void EnterTargetingState(ActionState state)
        {
            CurrentState = state;
        }

        public void SetPendingCard(Card? card)
        {
            PendingCard = card;
        }

        public void ResetTargetingToNormal() => ClearState();
    }
}


