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
        /// True while a cross-player forced-discard sequence (Neogi) is in progress - i.e.
        /// between EndTurn() beginning that sequence and the last opponent's discard
        /// resolving. DiscardCardCommand checks this to route a resolved discard back into
        /// ResolveOpponentDiscard instead of the normal ActionSystem.CompleteAction() chain-
        /// continuation path.
        /// </summary>
        bool IsResolvingOpponentDiscard { get; }

        /// <summary>
        /// Advances the in-progress opponent-discard sequence with the card just discarded -
        /// dequeues, discards, and either moves to the next opponent or (queue empty)
        /// completes the deferred end-of-turn player-switch. Only meaningful while
        /// IsResolvingOpponentDiscard is true.
        /// </summary>
        void ResolveOpponentDiscard(Card discardedCard);

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



