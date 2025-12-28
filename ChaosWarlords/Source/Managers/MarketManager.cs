using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Managers
{
    public class MarketManager : IMarketManager
    {
        private readonly ICardDatabase _cardDatabase;
        private readonly List<Card> _marketDeck;

        public List<Card> MarketRow { get; private set; }

        public MarketManager(ICardDatabase cardDatabase, IGameRandom random = null)
        {
            _cardDatabase = cardDatabase;
            _marketDeck = _cardDatabase.GetAllMarketCards();
            MarketRow = new List<Card>();

            if (random != null)
                random.Shuffle(_marketDeck);
            else
                _marketDeck.Shuffle(); // Fallback to System.Random logic if no seed provided (legacy/test support)

            RefillMarket();
        }

        public bool TryBuyCard(Player player, Card card, IPlayerStateManager stateManager)
        {
            if (!MarketRow.Contains(card)) return false;

            // Use PlayerStateManager for Resource Check & Spend
            if (!stateManager.TrySpendInfluence(player, card.Cost)) return false;

            // Remove from Market
            MarketRow.Remove(card);

            // Add to Player via StateManager
            stateManager.AcquireCard(player, card);

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

        // Removed ShuffleDeck private method as it's handled in constructor now

    }
}


