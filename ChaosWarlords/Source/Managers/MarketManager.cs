using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Systems
{
    public class MarketManager : IMarketManager
    {
        private readonly ICardDatabase _cardDatabase;
        private readonly List<Card> _marketDeck;

        public List<Card> MarketRow { get; private set; }

        public MarketManager(ICardDatabase cardDatabase)
        {
            _cardDatabase = cardDatabase;
            _marketDeck = _cardDatabase.GetAllMarketCards();
            MarketRow = new List<Card>();

            ShuffleDeck(_marketDeck);
            RefillMarket();
        }

        public bool TryBuyCard(Player player, Card card)
        {
            if (!MarketRow.Contains(card)) return false;
            if (player.Influence < card.Cost) return false;

            player.Influence -= card.Cost;
            MarketRow.Remove(card);
            player.DeckManager.AddToDiscard(card);

            RefillMarket();
            return true;
        }

        private void RefillMarket()
        {
            while (MarketRow.Count < GameConstants.MARKET_ROW_SIZE && _marketDeck.Count > 0)
            {
                Card card = _marketDeck[0];
                _marketDeck.RemoveAt(0);
                card.Location = CardLocation.Market;
                MarketRow.Add(card);
            }
        }

        private void ShuffleDeck(List<Card> deck)
        {
            deck.Shuffle(); // Use extension method from CollectionHelpers
        }

    }
}