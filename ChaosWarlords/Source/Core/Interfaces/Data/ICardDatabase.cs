using System.Collections.Generic;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Core.Interfaces.Data
{
    public interface ICardDatabase
    {
        /// <summary>
        /// Returns all cards available for the Market Deck.
        /// </summary>
        List<Card> GetAllMarketCards();

        /// <summary>
        /// Retrieves a specific card definition by its ID (useful for networking/modding).
        /// </summary>
        Card? GetCardById(string id);
    }
}



