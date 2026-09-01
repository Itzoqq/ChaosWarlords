using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Interfaces.Logic
{
    /// <summary>
    /// Coordinates high-level gameplay actions, targeting flow, and action resolution.
    /// Acts as the central hub for user-initiated mechanics (Assassinate, Deploy, Card Play).
    /// </summary>
    public interface IActionSystem
    {
        /// <summary>
        /// Fired when an action successfully resolves.
        /// </summary>
        event EventHandler OnActionCompleted;

        /// <summary>
        /// Fired when an action fails validation or execution, providing a reason string.
        /// </summary>
        event EventHandler<string> OnActionFailed;

        /// <summary>
        /// Fired when the system auto-generates a command (e.g. from Pre-Targeting) that needs execution.
        /// </summary>
        event Action<IGameCommand> OnAutoExecuteCommand;

        /// <summary>
        /// Fired when the logic layer needs a player decision on an optional card effect
        /// (e.g. an accept/decline popup). Decouples ActionSystem from the UI layer: it
        /// raises this plain event instead of calling into IUIEventMediator directly: the
        /// UI layer (UIEventMediator) subscribes and calls request.OnResponse(bool) with
        /// the player's answer.
        /// </summary>
        event Action<Contexts.InteractionRequest> OnInteractionRequested;

        /// <summary>
        /// Gets the current state of the action state machine (e.g. Normal, SelectingTarget).
        /// </summary>
        ActionState CurrentState { get; }
        event EventHandler<ActionState> OnStateChanged;

        /// <summary>
        /// Gets the card involved in the current pending action, if any.
        /// </summary>
        Card? PendingCard { get; }

        /// <summary>
        /// Gets the sites involved in the current pending action, if any.
        /// </summary>
        Site? PendingSite { get; }

        /// <summary>
        /// Gets the source node for a move action sequence.
        /// </summary>
        MapNode? PendingMoveSource { get; }

        /// <summary>
        /// Initiates the Assassination action flow.
        /// </summary>
        void TryStartAssassinate();

        /// <summary>
        /// Initiates the Return Spy action flow.
        /// </summary>
        void TryStartReturnSpy();

        /// <summary>
        /// Transitions the system into a targeting mode for a specific action.
        /// </summary>
        /// <param name="state">The target action state (e.g. SelectingSpyToReturn).</param>
        /// <param name="card">The card initiating this action, if applicable.</param>
        void StartTargeting(ActionState state, Card? card = null);

        /// <summary>
        /// Transititions the system to the Spy Selection state for the given site.
        /// </summary>
        void TransitionToSpySelection(Site site);

        /// <summary>
        /// Sets PendingSite ONLY, without touching CurrentState - unlike
        /// TransitionToSpySelection, which also forces CurrentState to
        /// SelectingSpyToReturn (correct for the enemy-spy flow, wrong here). Used by
        /// ReturnOwnSpyCommand so a chained effect (e.g. Cloaker's Assassinate, scoped to
        /// "at that spy's site") can read back which site the spy was just returned from.
        /// </summary>
        void SetPendingSiteForChain(Site site);

        /// <summary>
        /// Notifies the system that an action has failed validation or execution,
        /// and cancels the current targeting sequence.
        /// </summary>
        void NotifyFailure(string reason);

        /// <summary>
        /// Raises the <see cref="OnActionFailed"/> event without cancelling the current targeting
        /// sequence. Use for retryable validation failures (e.g. "invalid target, pick another"),
        /// as opposed to <see cref="NotifyFailure"/> which cancels.
        /// </summary>
        void RaiseActionFailed(string reason);

        /// <summary>
        /// Sets the source node for a Move Troop action sequence and transitions to the
        /// destination-targeting state.
        /// </summary>
        void SetMoveSource(MapNode? node);

        /// <summary>
        /// Cancels the current targeting sequence and returns to Normal state.
        /// </summary>
        void CancelTargeting();

        /// <summary>
        /// Checks if the system is currently in a targeting state.
        /// </summary>
        /// <returns>True if expecting user input for a target; otherwise, false.</returns>
        bool IsTargeting();

        /// <summary>
        /// Finalizes the pending action, validating and executing the logic.
        /// </summary>
        void CompleteAction();

        /// <summary>
        /// Handles a click on a map node, advancing the state machine if valid.
        /// </summary>
        /// <param name="targetNode">The node clicked.</param>
        /// <param name="targetSite">The specific site within the node (unused if node-level action).</param>
        IGameCommand? HandleTargetClick(MapNode? targetNode, Site? targetSite);

        /// <summary>
        /// Completes the Return Spy action for a specific selected spy color.
        /// </summary>
        /// <param name="selectedSpyColor">The faction color of the spy to return.</param>
        IGameCommand? FinalizeSpyReturn(PlayerColor selectedSpyColor);



        /// <summary>
        /// Injects the PlayerStateManager dependency.
        /// Use to break circular dependencies between Managers and ActionSystem.
        /// </summary>
        /// <param name="stateManager">The manager instance.</param>
        void SetPlayerStateManager(IPlayerStateManager stateManager);
        void SetMatchManager(IMatchManager matchManager);
        void SetMarketManager(IMarketManager marketManager);
        void SetMarketStateManager(IMarketStateManager manager);

        /// <summary>
        /// The card selected for Devouring, pending final execution of the chain.
        /// </summary>
        Card? PendingDevourCard { get; }



        /// <summary>
        /// Initiates the Devour Hand action flow (clearing hand/resources).
        /// </summary>
        /// <param name="sourceCard">The card triggering the devour effect.</param>
        /// <param name="deferExecution">If true, the devour action is not executed immediately but stored.</param>
        void TryStartDevourHand(Card sourceCard, Action? onComplete = null, bool deferExecution = false);

        /// <summary>
        /// Initiates the Devour Market action flow.
        /// </summary>
        void TryStartDevourMarket(Card sourceCard, Action? onComplete = null, bool deferExecution = false);

        /// <summary>
        /// Handles the selection of a hand card to devour.
        /// </summary>
        Commands.DevourCardCommand? HandleDevourSelection(Card? targetCard);

        /// <summary>
        /// Handles the selection of a market card to devour.
        /// </summary>
        Commands.DevourCardCommand? HandleDevourMarketSelection(Card? targetCard);

        /// <summary>
        /// Initiates the Devour Inner Circle action flow.
        /// </summary>
        void TryStartDevourInnerCircle(Card sourceCard, Action? onComplete = null, bool deferExecution = false);

        /// <summary>
        /// Handles the selection of an inner circle card to devour.
        /// </summary>
        Commands.DevourCardCommand? HandleDevourInnerCircleSelection(Card? targetCard);

        /// <summary>
        /// Explicitly sets the pending devour card (deferral).
        /// </summary>
        void DeferDevour(Card card);



        // --- Perform Methods (Exposed for Replay Commands) ---
        void PerformAssassinate(MapNode node, string? cardId, string? devourCardId = null);
        void PerformReturnTroop(MapNode node, string? cardId);
        void PerformSupplant(MapNode node, string? cardId, string? devourCardId = null);
        void PerformPlaceSpy(Site site, string? cardId);
        bool PerformSpyReturn(Site site, PlayerColor selectedSpyColor, string? cardId);
        void PerformMoveTroop(MapNode source, MapNode dest, string? cardId);


        /// <summary>
        /// Initiates the Supplant action flow, checking for pre-targets.
        /// </summary>
        void TryStartSupplant(Card sourceCard);

        /// <summary>
        /// Advances the targeting state to the next necessary effect for a Pre-Commit card play.
        /// </summary>
        /// <returns>True if advanced to a new state; False if no more targeting is needed.</returns>
        bool AdvancePreCommitTargeting(Card sourceCard);

        /// <summary>
        /// Stores a pre-selected target for a card to prevent re-entering targeting mode during resolution.
        /// </summary>
        void SetPreTarget(Card source, ActionState forState, object target);

        /// <summary>
        /// Retrieves and consumes the pre-selected target for a card.
        /// </summary>
        object? GetAndClearPreTarget(Card source, ActionState forState);

        /// <summary>
        /// The stack of pending effects to be resolved.
        /// Replaces linear recursion with an iterative stack for multiplayer compatibility.
        /// </summary>
        Stack<Contexts.EffectContext> ExecutionStack { get; }

        /// <summary>
        /// Pushes a new effect onto the execution stack.
        /// </summary>
        void PushEffect(Contexts.EffectContext context);

        /// <summary>
        /// Peeks the current effect from the stack.
        /// </summary>
        Contexts.EffectContext? CurrentEffect { get; }

        /// <summary>
        /// Processes the stack. If the top item requires input, it waits.
        /// If the top item is automatic (e.g. GainResource), it executes immediately and pops.
        /// </summary>
        void ProcessStack();

        /// <summary>
        /// Explicitly resolves the current top-of-stack effect (e.g. after a valid command is received).
        /// </summary>
        /// <param name="success">Whether the effect resolved successfully.</param>
        void ResolveCurrentEffect(bool success);

        /// <summary>
        /// Restore-only: overwrites CurrentState and the Pending* fields directly, bypassing
        /// the normal targeting-flow mutators (StartAssassinate, HandleReturnSpyInitialClick,
        /// etc.) and the side effects some of those carry. Exists solely for
        /// StateRestorer.RestoreState to put ActionSystem's targeting state machine back to a
        /// pre-command snapshot on rollback - not a general-purpose setter, and not something
        /// game logic outside StateRestorer should call. See GameStateDto.ActionSystemState.
        /// </summary>
        void RestorePendingState(ActionState state, Card? pendingCard, Site? pendingSite, MapNode? pendingMoveSource, Card? pendingDevourCard);

        // --- Engine-only methods (ActionExecutionEngine's exclusive callers) ---
        // Narrow, single-purpose targeting-state mutators that stack-processing needs to
        // trigger now that it lives in a separate class (ActionExecutionEngine) from the
        // targeting state machine itself. Same convention RestorePendingState already
        // established: not intended for external callers (UI, commands, tests driving real
        // gameplay) - StartTargeting/CancelTargeting/TryStart* are the real public entry
        // points for player-initiated targeting. See ActionExecutionEngine's doc comment
        // and planning.txt.

        /// <summary>Sets CurrentState directly, with none of StartTargeting's pre-target
        /// auto-execution side effects.</summary>
        void EnterTargetingState(ActionState state);

        /// <summary>Sets PendingCard directly, without touching CurrentState.</summary>
        void SetPendingCard(Card? card);

        /// <summary>Equivalent to ActionSystem's own ClearState() - resets CurrentState to
        /// Normal and clears PendingCard/PendingSite/PendingMoveSource (not PendingDevourCard,
        /// which deliberately survives across chained actions - see ClearState's own doc
        /// comment).</summary>
        void ResetTargetingToNormal();
    }
}



