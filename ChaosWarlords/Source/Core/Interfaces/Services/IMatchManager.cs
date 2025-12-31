using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// High-level coordinator for match flow and rule enforcement.
    /// Serves as the primary entry point for gameplay actions that affect validity and game state.
    /// </summary>
    public interface IMatchManager
    {
        /// <summary>
        /// Attempts to play a card, triggering its effects and consuming resources.
        /// </summary>
        /// <param name="card">The card to play.</param>
        void PlayCard(Card card);

        /// <summary>
        /// Permanently removes a card from the game (devour mechanic).
        /// </summary>
        /// <param name="card">The card to devour.</param>
        void DevourCard(Card card);

        /// <summary>
        /// Moves a card from the active area (Hand) to the Played area.
        /// </summary>
        /// <param name="card">The card to move.</param>
        void MoveCardToPlayed(Card card);

        /// <summary>
        /// Checks if the current turn can be legally ended.
        /// </summary>
        /// <param name="reason">Output parameter describing why the turn cannot end, if applicable.</param>
        /// <returns>True if the turn can end; otherwise, false.</returns>
        bool CanEndTurn(out string reason);

        /// <summary>
        /// Formally ends the current turn, performing cleanup and passing control.
        /// </summary>
        void EndTurn();

        /// <summary>
        /// Checks if the game has ended due to victory conditions.
        /// </summary>
        /// <returns>True if the game is over; otherwise, false.</returns>
        bool IsGameOver();

        /// <summary>
        /// Triggers the game over state and final scoring.
        /// </summary>
        void TriggerGameOver();
    }
}



