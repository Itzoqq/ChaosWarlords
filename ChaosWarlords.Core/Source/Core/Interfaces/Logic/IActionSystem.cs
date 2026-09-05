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
        /// Gets the site involved in the current pending action, if any - "the site
        /// associated with the current effect chain". Two distinct uses share this one
        /// field: (1) scoping a LATER chained step to the site an earlier step targeted
        /// (e.g. Cloaker's own-spy-return-then-assassinate: see
        /// ActionInputController.HandleAssassinate's PendingSite check), and (2) a
        /// Condition-evaluation READ of the site a step just targeted, checked while that
        /// same step's OnSuccess is resolving (e.g. Banshee/Infiltrator's
        /// ConditionType.OpponentPresentAtSite - see PlaceSpyCommand.Execute, which sets
        /// this via SetPendingSiteForChain right before CompleteAction() resolves the
        /// PlaceSpy effect and its OnSuccess child). Already has full DTO/rollback support
        /// (GameStateDto.PendingSiteId, DtoMapper, StateRestorer) and is cleared by
        /// ClearState() on every return to Normal.
        /// </summary>
        Site? PendingSite { get; }

        /// <summary>
        /// Gets the source node for a move action sequence.
        /// </summary>
        MapNode? PendingMoveSource { get; }

        /// <summary>
        /// The color of the player whose troop/spy was just removed by the most recently
        /// resolved Assassinate/Supplant step (set in PerformAssassinate/PerformSupplant right
        /// before the map mutation that would otherwise erase it), or null if no such step has
        /// resolved yet this sequence. Outcome-dependent targeting's read side (e.g.
        /// Mindwitness: "if that troop belonged to another player... they must discard a
        /// card") - see CardEffect.TargetsAffectedPlayer and
        /// CardEffectProcessor.PushEffectContext, which resolves this into the actual Player an
        /// OnSuccess/Alternative chain step should target. Cleared by ClearState() alongside
        /// PendingSite. Has full DTO/rollback support (GameStateDto.PendingAffectedPlayerColor,
        /// DtoMapper, StateRestorer), matching PendingSite's pattern.
        /// </summary>
        PlayerColor? PendingAffectedPlayerColor { get; }

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
        /// Captures a full-state snapshot for CancelTargeting() to restore to, if this is
        /// genuinely the start of a new sequence (CurrentState == Normal) and one hasn't
        /// already been taken for it. Idempotent and safe to call from multiple entry
        /// points - MatchManager.PlayCard/PlayCardFromMarket call this BEFORE resolving a
        /// card's effects (not just when a targeting UI actually opens), because a card
        /// shaped "automatic mutation, THEN mandatory targeting" (e.g. Matron Mother:
        /// MoveDeckToDiscard -> PromoteFromPile; Cranium Rats: GainResource -> SelectOpponent)
        /// already mutates state before StartTargeting/EnterTargetingState ever runs - by
        /// then it's too late to snapshot the pre-mutation state. See planning.txt's
        /// CancelTargeting/EnterTargetingState gap writeup.
        /// </summary>
        void EnsureTargetingSnapshot();

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
        /// SetMatchManager/SetMarketStateManager exist to break a genuine circular dependency:
        /// both arrive later, from the client layer (GameplayState.cs), only after
        /// MatchContext/MatchManager/MarketStateManager exist, which themselves need
        /// ActionSystem to already exist first. IPlayerStateManager/IMarketManager used to be
        /// setters here too, but both are actually available at construction time (see
        /// MatchFactory.SetupActionSystem) - required ActionSystem constructor params instead.
        /// </summary>
        void SetMatchManager(IMatchManager matchManager);
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
        /// Initiates the "play a market card as if in hand" flow (e.g. Ulitharid) - opens the
        /// market for selection (reusing the same IMarketStateManager.OpenForDevour mechanism
        /// Devour-from-Market uses; the underlying "pick a market card, get a command back
        /// from a callback" shape is identical) and enters TargetingPlayFromMarket.
        /// </summary>
        /// <param name="sourceCard">The card triggering this (e.g. Ulitharid).</param>
        /// <param name="maxCost">The maximum cost of a market card that can be selected.</param>
        void TryStartPlayFromMarket(Card sourceCard, int maxCost);

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

        /// <summary>
        /// Handles the selection of a card to promote via EffectType.PromoteFromPile's
        /// immediate flow (e.g. Matron Mother, Necromancer). Returns a PromoteCommand with
        /// IsChainedEffect set so its Execute() pops the blocking EffectContext it's resolving,
        /// mirroring HandleDevourInnerCircleSelection's shape.
        /// </summary>
        Commands.PromoteCommand? HandlePromoteFromPileSelection(Card? targetCard);



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
        /// The CardEffect currently being resolved on top of the execution stack (the effect that
        /// caused the current blocking targeting state), or null if there is no such effect (e.g. a
        /// directly-dispatched, non-card-driven basic action). Lets click-handling/command
        /// validation read effect-specific targeting constraints (e.g. CardEffect.
        /// TargetNeutralTroopOnly) without threading a new parameter through every targeting path.
        /// </summary>
        CardEffect? CurrentSourceEffect { get; }

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
        void RestorePendingState(ActionState state, Card? pendingCard, Site? pendingSite, MapNode? pendingMoveSource, Card? pendingDevourCard, PlayerColor? pendingAffectedPlayerColor = null);

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



