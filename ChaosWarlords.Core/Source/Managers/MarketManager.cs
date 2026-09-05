using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    public class MarketManager : IMarketManager
    {
        private readonly ICardDatabase _cardDatabase;

        public List<Card> MarketRow { get; private set; }
        public List<Card> MarketDeck { get; }

        public MarketManager(ICardDatabase cardDatabase, IGameRandom random)
        {
            _cardDatabase = cardDatabase;
            MarketDeck = _cardDatabase.GetAllMarketCards(random);
            MarketRow = new List<Card>();

            // Shuffle market deck using deterministic RNG
            random.Shuffle(MarketDeck);

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
            while (MarketRow.Count < GameConstants.MarketRowSize && MarketDeck.Count > 0)
            {
                Card card = MarketDeck[0];
                MarketDeck.RemoveAt(0);
                card.Location = CardLocation.Market;
                MarketRow.Add(card);
            }
        }

        public void RemoveCard(Card card)
        {
            if (MarketRow.Remove(card))
            {
                RefillMarket();
            }
        }

        public bool HasCardsInDeck()
        {
            return MarketDeck.Count > 0;
        }

        public void ReplaceCard(Card target, Card replacement)
        {
            int index = MarketRow.IndexOf(target);
            if (index != -1)
            {
                MarketRow[index] = replacement;
                replacement.Location = CardLocation.Market;
                // Target is removed from the row, but its extensive cleanup (location=void) 
                // is handled by the caller (MarketManager handles the Collection, Caller handles Logic/State).
            }
        }

        // Removed ShuffleDeck private method as it's handled in constructor now

    }
}


