using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    /// <summary>
    /// Manages the deck lifecycle: Draw, Discard, and Reshuffle.
    /// Encapsulates the randomization and recycling logic.
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _drawPile = [];
        private readonly List<Card> _discardPile = [];

        // Public read-only access if needed for UI/Debugging
        public IReadOnlyList<Card> DrawPile => _drawPile.AsReadOnly();
        public IReadOnlyList<Card> DiscardPile => _discardPile.AsReadOnly();

        public int Count => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        public Deck() { }

        // Initialize with a set of cards
        public Deck(IEnumerable<Card> cards)
        {
            _drawPile.AddRange(cards);
        }

        /// <summary>
        /// Adds a card to the discard pile.
        /// </summary>
        /// <param name="card">The card to discard.</param>
        public void AddToDiscard(Card card)
        {
            if (card is not null)
            {
                card.Location = CardLocation.DiscardPile;
                _discardPile.Add(card);
            }
        }

        /// <summary>
        /// Adds a collection of cards to the discard pile.
        /// </summary>
        /// <param name="cards">The cards to discard.</param>
        public void AddToDiscard(IEnumerable<Card> cards)
        {
            foreach (var card in cards)
            {
                AddToDiscard(card);
            }
        }

        // For setup or special effects that add directly to deck
        /// <summary>
        /// Adds a card to the top of the draw pile.
        /// </summary>
        /// <param name="card">The card to add.</param>
        public void AddToTop(Card card)
        {
            if (card is not null)
            {
                card.Location = CardLocation.Deck;
                _drawPile.Insert(0, card);
            }
        }

        /// <summary>
        /// Adds a card directly to the draw pile without preserving normal placement rules.
        /// Used for restoring deck state from a snapshot (e.g. transactional rollback).
        /// </summary>
        /// <param name="card">The card to add.</param>
        public void ForceAdd(Card card)
        {
            if (card is not null)
            {
                card.Location = CardLocation.Deck;
                _drawPile.Add(card);
            }
        }

        /// <summary>
        /// Removes all cards from the draw pile.
        /// </summary>
        public void Clear()
        {
            _drawPile.Clear();
        }

        /// <summary>
        /// Removes all cards from the discard pile.
        /// </summary>
        public void ClearDiscard()
        {
            _discardPile.Clear();
        }

        /// <summary>
        /// Moves every card currently in the draw pile into the discard pile (e.g. Matron
        /// Mother's "Put your deck into your discard pile"). Mirrors ReshuffleDiscard's style
        /// for mutating Location/the two lists, just in the opposite direction.
        /// </summary>
        public void MoveAllToDiscard()
        {
            foreach (var card in _drawPile)
            {
                card.Location = CardLocation.DiscardPile;
            }
            _discardPile.AddRange(_drawPile);
            _drawPile.Clear();
        }

        /// <summary>
        /// Removes a specific card from the discard pile (e.g. EffectType.PromoteFromPile
        /// promoting a card out of the discard pile). Returns false if the card isn't there.
        /// </summary>
        /// <param name="card">The card to remove.</param>
        public bool RemoveFromDiscard(Card card) => _discardPile.Remove(card);

        public List<Card> Draw(int count, IGameRandom random)
        {
            var drawnCards = new List<Card>();

            for (int i = 0; i < count; i++)
            {
                if (_drawPile.Count == 0)
                {
                    ReshuffleDiscard(random);
                    if (_drawPile.Count == 0) break; // Still empty? Stop drawing.
                }

                Card card = _drawPile[0];
                _drawPile.RemoveAt(0);
                drawnCards.Add(card);
            }

            return drawnCards;
        }

        /// <summary>
        /// Shuffles the draw pile using the provided random number generator.
        /// </summary>
        /// <param name="random">The random number generator to use for shuffling.</param>
        public void Shuffle(IGameRandom random)
        {
            ArgumentNullException.ThrowIfNull(random);

            random.Shuffle(_drawPile);
        }

        private void ReshuffleDiscard(IGameRandom random)
        {
            if (_discardPile.Count > 0)
            {
                // Move discard to deck
                foreach (var card in _discardPile)
                {
                    card.Location = CardLocation.Deck;
                }
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();

                // Shuffle
                Shuffle(random);
            }
        }
    }
}


