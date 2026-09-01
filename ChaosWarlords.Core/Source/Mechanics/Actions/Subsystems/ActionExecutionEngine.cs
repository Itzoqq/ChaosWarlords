using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    /// <summary>
    /// Owns ActionSystem's execution-stack machinery: ExecutionStack itself, and the push/
    /// resolve/process cycle that walks it (PushEffect, ResolveCurrentEffect, ProcessStack,
    /// and everything ProcessStack calls into - optional-effect confirmation, automatic-effect
    /// application, pre-target auto-execution). Split out of ActionSystem 2026-08-31 so that
    /// class isn't ALSO the targeting state machine's home - CurrentState/PendingCard/
    /// PendingSite/PendingMoveSource/CancelTargeting stay on ActionSystem, since that's the
    /// half external callers (input modes, commands, UI) actually query and react to, and
    /// every "Perform*" command method needs it directly. ActionSystem still implements the
    /// full IActionSystem contract unchanged and delegates ExecutionStack/PushEffect/
    /// CurrentEffect/ProcessStack/ResolveCurrentEffect straight through to an instance of this
    /// class it owns - the same composition pattern already used for DevourSubsystem/
    /// SpySubsystem/ActionInputController/PreTargetHandler, just for one more slice of what
    /// used to be one 854-line file. See planning.txt.
    ///
    /// Like DevourSubsystem/SpySubsystem, this takes IActionSystem itself as a collaborator
    /// (not the concrete ActionSystem) and calls back into it for the handful of targeting-
    /// state transitions stack-processing needs to trigger - EnterTargetingState/
    /// SetPendingCard/ResetTargetingToNormal, narrow doc-commented "engine-only" methods on
    /// IActionSystem (the same convention RestorePendingState already established).
    /// OnActionCompleted/OnInteractionRequested/OnAutoExecuteCommand can't be raised that way
    /// though - C# events are only invocable from their declaring type, even through an
    /// interface reference - so this class declares its own copies of those three, which
    /// ActionSystem subscribes to in its constructor and re-raises as its own public events.
    /// IActionSystem's contract is completely unchanged for every existing caller; this is an
    /// implementation-detail extraction, not an API change.
    /// </summary>
    internal class ActionExecutionEngine
    {
        public event EventHandler? OnActionCompleted;
        public event Action<Core.Contexts.InteractionRequest>? OnInteractionRequested;
        public event Action<IGameCommand>? OnAutoExecuteCommand;

        public Stack<Core.Contexts.EffectContext> ExecutionStack { get; } = new();
        public Core.Contexts.EffectContext? CurrentEffect => ExecutionStack.Count > 0 ? ExecutionStack.Peek() : null;

        private readonly IActionSystem _actionSystem;
        private readonly IGameLogger _logger;
        private readonly Managers.PreTargetHandler _preTargetHandler;
        private Contexts.MatchContext? _matchContext;

        public ActionExecutionEngine(IActionSystem actionSystem, IGameLogger logger, Managers.PreTargetHandler preTargetHandler)
        {
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _preTargetHandler = preTargetHandler ?? throw new ArgumentNullException(nameof(preTargetHandler));
        }

        public void SetMatchContext(Contexts.MatchContext context)
        {
            _matchContext = context;
        }

        public void PushEffect(Core.Contexts.EffectContext context)
        {
            ExecutionStack.Push(context);
            _logger.Log($"ActionExecutionEngine: Pushed Effect [{context.EffectType}] from {context.SourceCard?.Name}. Stack Size: {ExecutionStack.Count}", LogChannel.Debug);
        }

        public void ResolveCurrentEffect(bool success)
        {
            if (ExecutionStack.Count == 0)
            {
                _logger.Log("ActionExecutionEngine: ResolveCurrentEffect called but stack is empty!", LogChannel.Warning);
                return;
            }

            var effect = ExecutionStack.Pop();
            _logger.Log($"ActionExecutionEngine: [RESOLVE] Popped effect [{effect.EffectType}] Success={success}. Remaining Stack: {ExecutionStack.Count}", LogChannel.Debug);
            _logger.Log($"ActionExecutionEngine: [RESOLVE] Effect has callback: {effect.OnResolved != null}, SourceEffect: {effect.SourceEffect?.Type}", LogChannel.Debug);

            if (success)
            {
                if (effect.OnResolved != null)
                {
                    _logger.Log($"ActionExecutionEngine: [RESOLVE] Invoking OnResolved callback for [{effect.EffectType}]...", LogChannel.Debug);
                    effect.OnResolved.Invoke(true);
                    _logger.Log($"ActionExecutionEngine: [RESOLVE] OnResolved callback completed. Stack now has {ExecutionStack.Count} items.", LogChannel.Debug);
                }
                else
                {
                    _logger.Log($"ActionExecutionEngine: [RESOLVE] No OnResolved callback for [{effect.EffectType}]", LogChannel.Debug);
                }
            }
            else
            {
                effect.OnCancelled?.Invoke();
            }

            // After popping, process the next item
            _logger.Log($"ActionExecutionEngine: [RESOLVE] Calling ProcessStack to continue. Stack size: {ExecutionStack.Count}", LogChannel.Debug);
            ProcessStack();
            _logger.Log($"ActionExecutionEngine: [RESOLVE] ProcessStack returned. Current state: {_actionSystem.CurrentState}", LogChannel.Debug);
        }

        public void ProcessStack()
        {
            _logger.Log($"ActionExecutionEngine: [PROCESS] ProcessStack called. Stack size: {ExecutionStack.Count}, Current state: {_actionSystem.CurrentState}", LogChannel.Debug);

            if (HandleStackEmptyState())
            {
                return;
            }

            var nextEffect = ExecutionStack.Peek();
            _logger.Log($"ActionExecutionEngine: [PROCESS] Next effect: [{nextEffect.EffectType}] RequiresInput={nextEffect.RequiresInput}, SourceEffect={nextEffect.SourceEffect?.Type}", LogChannel.Debug);

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
                _logger.Log("ActionExecutionEngine: [PROCESS] Stack Empty. Sequence Complete.", LogChannel.Debug);
                OnActionCompleted?.Invoke(this, EventArgs.Empty);
                _actionSystem.ResetTargetingToNormal();
                return true;
            }
            return false;
        }

        private void HandleInputRequiredEffect(Core.Contexts.EffectContext nextEffect)
        {
            // Effect requires user input
            _actionSystem.SetPendingCard(nextEffect.SourceCard);
            bool isOptional = nextEffect.SourceEffect?.IsOptional == true;

            // A mandatory (non-optional) Devour effect needs its strategy's own Execute()
            // called, not just a bare state change - SetupTargetingForRequiredEffect only
            // sets CurrentState, but Devour(Market) also needs
            // IMarketStateManager.OpenForDevour called (the UI's market picker never opens
            // otherwise), and Devour(Self) needs to resolve immediately (no real target to
            // pick - DevourStrategy.GetTargetingState even returns ActionState.Normal for it,
            // not a real targeting state). The optional case already reaches the identical
            // DevourStrategyFactory call via HandleOptionalEffectAccepted below; this is the
            // same call for the MANDATORY case (e.g. Carrion Crawler's Devour(Market) - found
            // to be completely unplayable without this, the market never opened; Insane
            // Outcast's own "discard -> devour self" chain), which would otherwise sit in
            // "waiting for input" forever with nothing to click. For Market/Hand/InnerCircle,
            // strategy.Execute() itself calls StartTargeting (and, for Market, opens the
            // market) and the pushed EffectContext stays on the stack, resolved later by
            // whatever command the eventual click produces calling CompleteAction() - the
            // onComplete callback here only matters for Self, which resolves synchronously
            // with no click at all.
            if (!isOptional && nextEffect.SourceEffect?.Type == EffectType.Devour)
            {
                var strategy = Mechanics.Rules.DevourStrategyFactory.GetStrategy(nextEffect.SourceEffect.TargetLocation);
                strategy.Execute(nextEffect.SourceCard, _matchContext!, _logger, () => ResolveCurrentEffect(true), false);
                return;
            }

            // Same shape, for "play a market card as if in hand" (Ulitharid): needs
            // IMarketStateManager.OpenForDevour called too, so it can't rely on a bare state
            // change either. Always needs a click (which market card), so - like Market/Hand/
            // InnerCircle Devour above - no onComplete callback here; the pushed EffectContext
            // stays on the stack until the eventual PlayFromMarketCommand calls CompleteAction().
            if (!isOptional && nextEffect.SourceEffect?.Type == EffectType.PlayFromMarket)
            {
                _actionSystem.TryStartPlayFromMarket(nextEffect.SourceCard, nextEffect.SourceEffect.Amount);
                return;
            }

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

            _logger.Log($"ActionExecutionEngine: Waiting for input for {nextEffect.EffectType}...", LogChannel.Input);
        }

        /// <summary>
        /// Sets up the action state for a required (non-optional) targeting effect.
        /// </summary>
        private void SetupTargetingForRequiredEffect(Core.Contexts.EffectContext effect)
        {
            _logger.Log($"ActionExecutionEngine: [PROCESS] Effect requires input. Setting state to [{effect.EffectType}]", LogChannel.Debug);
            _actionSystem.EnterTargetingState(effect.EffectType);
            _logger.Log($"ActionExecutionEngine: [PROCESS] State set to: {_actionSystem.CurrentState}", LogChannel.Debug);
        }

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
                        _logger.Log($"ActionExecutionEngine: Skipping optional effect {effect.SourceEffect.Type} - OnSuccess effect {onSuccessEffect.Type} has no valid targets.", LogChannel.Warning);
                        ResolveCurrentEffect(false);
                        return true;
                    }
                }
            }

            _logger.Log($"ActionExecutionEngine: Requesting optional effect confirmation for {effect.SourceEffect?.Type}...", LogChannel.Input);

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
            _logger.Log($"ActionExecutionEngine: Optional effect {effect.SourceEffect?.Type} accepted.", LogChannel.Input);

            // User accepted - NOW we set the state to Targeting (if applicable)
            if (_actionSystem.CurrentState == ActionState.Normal && effect.EffectType != ActionState.Normal)
            {
                _actionSystem.EnterTargetingState(effect.EffectType);
                _logger.Log($"ActionExecutionEngine: [PROCESS] Optional Accepted -> State set to: {_actionSystem.CurrentState}", LogChannel.Debug);
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
            _logger.Log($"ActionExecutionEngine: Optional effect {effect.SourceEffect?.Type} declined.", LogChannel.Input);
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
                _actionSystem.HandleTargetClick,
                _actionSystem.HandleDevourSelection,
                cmd => OnAutoExecuteCommand?.Invoke(cmd),
                () => ResolveCurrentEffect(false)
            );

            if (executed)
            {
                _logger.Log($"ActionExecutionEngine: Pre-Target executed for {effect.EffectType}. Continuing stack...", LogChannel.Debug);
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
    }
}
