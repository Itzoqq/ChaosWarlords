using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Interfaces.Data
{
    public interface ICardDatabase
    {
        /// <summary>
        /// Returns all cards available for the Market Deck.
        /// </summary>
        List<Card> GetAllMarketCards(IGameRandom? random = null);

        /// <summary>Returns only cards belonging to the two selected market aspects.</summary>
        List<Card> GetMarketCards(MarketDeckSelection selection, IGameRandom? random = null);

        /// <summary>Creates the finite face-up recruit piles configured by card data.</summary>
        List<Entities.Cards.FixedRecruitPile> GetFixedRecruitPiles(IGameRandom? random = null) => [];

        /// <summary>
        /// Which of the 6 physical market half-decks currently sum to a real, complete 40-card
        /// count in the loaded catalog (rulebook p.4) - i.e. are actually safe to offer for a
        /// real match today. A half-deck still mid-transcription (planning.txt TIER 4 item 28)
        /// reports false here even though some of its cards already exist in cards.json. Callers
        /// (MatchSetupState) trust this result at face value, including an empty one - an empty
        /// result is the correct fail-closed answer for a genuinely broken catalog, not a signal
        /// to fall back to "anything goes." Defaults to "every half-deck is complete" only for an
        /// implementer with no completeness concept of its own at all (same permissive-default
        /// pattern as GetFixedRecruitPiles) - anything that DOES track completeness must report
        /// its real answer, empty or not.
        /// </summary>
        IReadOnlySet<MarketHalfDeck> GetCompleteHalfDecks() => new HashSet<MarketHalfDeck>(Enum.GetValues<MarketHalfDeck>());

        /// <summary>
        /// Retrieves a specific card definition by its ID (useful for networking/modding).
        /// </summary>
        Card? GetCardById(string id, IGameRandom? random = null);
    }
}



