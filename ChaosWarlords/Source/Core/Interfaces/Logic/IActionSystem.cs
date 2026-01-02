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
        ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand? HandleTargetClick(MapNode? targetNode, Site? targetSite);

        /// <summary>
        /// Completes the Return Spy action for a specific selected spy color.
        /// </summary>
        /// <param name="selectedSpyColor">The faction color of the spy to return.</param>
        ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand? FinalizeSpyReturn(PlayerColor selectedSpyColor);



        /// <summary>
        /// Injects the PlayerStateManager dependency.
        /// Use to break circular dependencies between Managers and ActionSystem.
        /// </summary>
        /// <param name="stateManager">The manager instance.</param>
        void SetPlayerStateManager(IPlayerStateManager stateManager);
        void SetMatchManager(IMatchManager matchManager);

        /// <summary>
        /// The card selected for Devouring, pending final execution of the chain.
        /// </summary>
        Card? PendingDevourCard { get; }

        /// <summary>
        /// Handles the selection of a card to devour.
        /// If deferExecution is true, the card is stored in PendingDevourCard and the success callback is invoked.
        /// </summary>
        void HandleDevourSelection(Card? targetCard);

        /// <summary>
        /// Initiates the Devour Hand action flow (clearing hand/resources).
        /// </summary>
        /// <param name="sourceCard">The card triggering the devour effect.</param>
        /// <param name="deferExecution">If true, the devour action is not executed immediately but stored.</param>
        void TryStartDevourHand(Entities.Cards.Card sourceCard, Action? onComplete = null, bool deferExecution = false);

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
        void SetPreTarget(Entities.Cards.Card source, ActionState forState, object target);

        /// <summary>
        /// Retrieves and consumes the pre-selected target for a card.
        /// </summary>
        object? GetAndClearPreTarget(Entities.Cards.Card source, ActionState forState);
    }
}



