using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Contexts;

namespace ChaosWarlords.Source.Core.Interfaces.Data
{
    public interface ICardDatabase
    {
        /// <summary>
        /// Returns all cards available for the Market Deck.
        /// </summary>
        List<Card> GetAllMarketCards(IGameRandom? random = null);

        /// <summary>Returns only cards belonging to the two selected market half-decks.</summary>
        List<Card> GetMarketCards(MarketDeckSelection selection, IGameRandom? random = null);

        /// <summary>Creates the finite face-up recruit piles configured by card data.</summary>
        List<Entities.Cards.FixedRecruitPile> GetFixedRecruitPiles(IGameRandom? random = null) => [];

        /// <summary>
        /// Retrieves a specific card definition by its ID (useful for networking/modding).
        /// </summary>
        Card? GetCardById(string id, IGameRandom? random = null);
    }
}



