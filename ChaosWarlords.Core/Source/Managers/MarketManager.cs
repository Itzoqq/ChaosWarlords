using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Contexts;

namespace ChaosWarlords.Source.Managers
{
    public class MarketManager : IMarketManager
    {
        private readonly ICardDatabase _cardDatabase;

        public List<Card> MarketRow { get; private set; }
        public List<Card> MarketDeck { get; }
        public List<FixedRecruitPile> FixedRecruitPiles { get; }

        public MarketManager(ICardDatabase cardDatabase, IGameRandom random, MarketDeckSelection? selection = null)
        {
            _cardDatabase = cardDatabase;
            MarketDeck = selection is null
                ? _cardDatabase.GetAllMarketCards(random)
                : _cardDatabase.GetMarketCards(selection, random)
                    ?? throw new InvalidOperationException("The card database returned no cards for the selected market half-decks.");
            MarketRow = new List<Card>();
            FixedRecruitPiles = _cardDatabase.GetFixedRecruitPiles(random) ?? [];

            // Shuffle market deck using deterministic RNG
            random.Shuffle(MarketDeck);

            RefillMarket();
        }

        public bool TryBuyCard(Player player, Card card, IPlayerStateManager stateManager)
        {
            bool isMarketRowCard = MarketRow.Contains(card);
            var fixedPile = FixedRecruitPiles.FirstOrDefault(pile => ReferenceEquals(pile.AvailableCard, card));
            if (!isMarketRowCard && fixedPile is null) return false;

            // Use PlayerStateManager for Resource Check & Spend
            if (!stateManager.TrySpendInfluence(player, card.Cost)) return false;

            // Remove from Market
            if (isMarketRowCard)
            {
                MarketRow.Remove(card);
            }
            else
            {
                fixedPile!.Cards.RemoveAt(0);
            }

            // Add to Player via StateManager
            stateManager.AcquireCard(player, card);

            if (isMarketRowCard) RefillMarket();
            return true;
        }

        public IEnumerable<Card> GetRecruitableCards()
        {
            return MarketRow.Concat(FixedRecruitPiles
                .Select(pile => pile.AvailableCard)
                .OfType<Card>());
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
