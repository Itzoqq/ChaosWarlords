using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Manages the game market, including the available card row and purchasing logic.
    /// </summary>
    public interface IMarketManager
    {
        /// <summary>
        /// Attempts to purchase a card from the market for a player.
        /// </summary>
        /// <param name="player">The player attempting the purchase.</param>
        /// <param name="card">The card to purchase.</param>
        /// <param name="stateManager">The state manager used to handle resource deduction and deck addition.</param>
        /// <returns>True if the purchase was successful; otherwise, false.</returns>
        bool TryBuyCard(Player player, Card card, IPlayerStateManager stateManager);

        /// <summary>
        /// Gets the current list of cards available for purchase in the market.
        /// </summary>
        List<Card> MarketRow { get; }

        /// <summary>
        /// Removes a specific card from the market row and triggers a refill.
        /// </summary>
        /// <param name="card">The card to remove.</param>
        void RemoveCard(Card card);

        /// <summary>
        /// Checks if there are any cards remaining in the market deck.
        /// Used for victory condition checking.
        /// </summary>
        /// <returns>True if the market deck has cards; otherwise, false.</returns>
        bool HasCardsInDeck();

        /// <summary>
        /// Replaces a target card in the market with a replacement card.
        /// </summary>
        /// <param name="target">The existing card in the market row to be replaced.</param>
        /// <param name="replacement">The new card to place in that slot.</param>
        void ReplaceCard(Card target, Card replacement);
    }
}



