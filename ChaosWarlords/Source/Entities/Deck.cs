using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Deck
    {
        private readonly List<Card> _drawPile = new List<Card>();
        private readonly List<Card> _discardPile = new List<Card>();

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

        public void AddToDiscard(Card card)
        {
            if (card != null)
            {
                card.Location = CardLocation.DiscardPile;
                _discardPile.Add(card);
            }
        }

        public void AddToDiscard(IEnumerable<Card> cards)
        {
            foreach (var card in cards)
            {
                AddToDiscard(card);
            }
        }
        
        // For setup or special effects that add directly to deck
        public void AddToTop(Card card)
        {
            if (card != null)
            {
                card.Location = CardLocation.Deck;
                _drawPile.Insert(0, card);
            }
        }

        public List<Card> Draw(int count)
        {
            var drawnCards = new List<Card>();

            for (int i = 0; i < count; i++)
            {
                if (_drawPile.Count == 0)
                {
                    ReshuffleDiscard();
                    if (_drawPile.Count == 0) break; // Still empty? Stop drawing.
                }

                Card card = _drawPile[0];
                _drawPile.RemoveAt(0);
                drawnCards.Add(card);
            }

            return drawnCards;
        }

        public void Shuffle()
        {
            ShuffleList(_drawPile);
        }

        private void ReshuffleDiscard()
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
                Shuffle();
            }
        }

        // Instance-based Random for thread safety (each deck has its own)
        private readonly Random _rng = new Random(Guid.NewGuid().GetHashCode());

        private void ShuffleList(List<Card> list)
        {
            list.Shuffle(); // Use extension method from CollectionHelpers
        }
    }
}
