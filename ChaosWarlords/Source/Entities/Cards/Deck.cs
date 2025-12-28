using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
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
            if (random == null)
                throw new ArgumentNullException(nameof(random));

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


