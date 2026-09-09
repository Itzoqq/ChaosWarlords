using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// High-level coordinator for match flow and rule enforcement.
    /// Serves as the primary entry point for gameplay actions that affect validity and game state.
    /// </summary>
    public interface IMatchManager
    {
        int RoundNumber { get; }
        int TotalTurnCount { get; }

        /// <summary>
        /// Attempts to play a card, triggering its effects and consuming resources.
        /// </summary>
        /// <param name="card">The card to play.</param>
        void PlayCard(Card card);

        /// <summary>
        /// Permanently removes a card from the game (devour mechanic).
        /// </summary>
        /// <param name="card">The card to devour.</param>
        /// <summary>
        /// Permanently removes a card from the game (devour mechanic).
        /// </summary>
        /// <param name="card">The card to devour.</param>
        void DevourCard(Card card, Card? sourceCard = null);

        /// <summary>
        /// Devours a card specifically from the Market, potentially replacing it with a source card.
        /// </summary>
        void DevourMarketCard(Card targetCard, Card? sourceCard);

        /// <summary>
        /// Moves a card from the active area (Hand) to the Played area.
        /// </summary>
        /// <param name="card">The card to move.</param>
        void MoveCardToPlayed(Card card);

        /// <summary>
        /// Plays a card sitting in the Market "as if it was in your hand" (e.g. Ulitharid),
        /// resolving its own effects (Focus computed off ITS aspect, not the source card's),
        /// then devouring it (removed from the market row, replaced from the market deck top -
        /// the standard Devour-from-Market removal, not a ReplaceWithSource-style swap). The
        /// market card never enters Hand or PlayedCards - stays Market throughout its own
        /// effect resolution, then goes straight to Void.
        /// </summary>
        /// <param name="marketCard">The market card to play (and then devour).</param>
        /// <param name="sourceCard">The card that triggered this (e.g. Ulitharid).</param>
        void PlayCardFromMarket(Card marketCard, Card sourceCard);

        /// <summary>
        /// Checks if the current turn can be legally ended.
        /// </summary>
        /// <param name="reason">Output parameter describing why the turn cannot end, if applicable.</param>
        /// <returns>True if the turn can end; otherwise, false.</returns>
        bool CanEndTurn(out string reason);

        /// <summary>
        /// Formally ends the current turn, performing cleanup and passing control. If any
        /// card played this turn forces opponents to discard (e.g. Neogi), this instead
        /// begins that sequence and defers the actual player-switch until it completes -
        /// see IsResolvingOpponentDiscard/ResolveOpponentDiscard.
        /// </summary>
        void EndTurn();

        /// <summary>
        /// True while a cross-player forced-discard sequence is in progress - either Neogi's
        /// end-of-turn "each opponent discards" phase, or a mid-turn reactive one queued via
        /// EnqueueReactiveDiscard (Umber Hulk) - both share the same underlying queue.
        /// DiscardCardCommand checks this (captured BEFORE applying a discarded card's own
        /// Card.ReactiveDiscardEffect, since that effect may itself enqueue a NEW entry into the
        /// same queue) to route a resolved discard back into ResolveOpponentDiscard instead of
        /// the normal ActionSystem.CompleteAction() chain-continuation path.
        /// </summary>
        bool IsResolvingOpponentDiscard { get; }

        /// <summary>
        /// Advances the in-progress opponent-discard sequence with the card just discarded -
        /// dequeues, discards, and either moves to the next player or (queue empty) completes
        /// the deferred end-of-turn player-switch (only if this phase was started from EndTurn -
        /// a mid-turn reactive-only phase just stops there instead). Only meaningful while
        /// IsResolvingOpponentDiscard is true.
        /// </summary>
        void ResolveOpponentDiscard(Card discardedCard);

        /// <summary>
        /// Queues <paramref name="player"/> to be immediately forced to discard a card, reusing
        /// the same queue/machinery as Neogi's end-of-turn phase (see IsResolvingOpponentDiscard)
        /// but started mid-turn instead - Umber Hulk's "if an opponent causes you to discard
        /// this, they must discard a card" (Card.ReactiveDiscardEffect). Does NOT start the
        /// targeting prompt synchronously - this is called from inside the CURRENT discard's own
        /// chain resolution, so starting one here would just get its CurrentState clobbered the
        /// instant ClearState() (which runs right after) resets it to Normal. The prompt actually
        /// starts via ResumeReactiveDiscardQueue instead.
        /// </summary>
        void EnqueueReactiveDiscard(Player player);

        /// <summary>
        /// Starts (or continues) the discard queue once a reactive entry (EnqueueReactiveDiscard)
        /// is the only thing left owning it - called from ActionSystem.
        /// ReleaseForcedActingPlayerIfOwnedByExecutionStack, the one point already positioned
        /// AFTER ClearState() has settled CurrentState back to Normal, so a fresh StartTargeting
        /// call here actually sticks. See that method's own doc comment for why nothing else is
        /// a safe place to do this.
        /// </summary>
        void ResumeReactiveDiscardQueue();

        /// <summary>
        /// Checks if the game has ended due to victory conditions.
        /// </summary>
        /// <returns>True if the game is over; otherwise, false.</returns>
        bool IsGameOver();

        /// <summary>
        /// Triggers the game over state and final scoring.
        /// </summary>
        void TriggerGameOver();

        /// <summary>
        /// The final victory data if the game is over.
        /// </summary>
        Core.Data.Dtos.VictoryDto? VictoryResult { get; }
        /// <summary>
        /// Resumes the execution of a card's effect chain (e.g. OnSuccess after targeting).
        /// </summary>
        void ResumeDevourChain(Card sourceCard);

        /// <summary>
        /// Gets the shared list of all cards that have been devoured (Void).
        /// </summary>
        IReadOnlyList<Card> VoidPile { get; }
    }
}



