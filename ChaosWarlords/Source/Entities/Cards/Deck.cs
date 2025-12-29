using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
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


