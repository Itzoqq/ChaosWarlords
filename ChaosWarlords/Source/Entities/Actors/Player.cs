using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Entities.Actors
{
    public class Player
    {
        // --- Identity ---

        /// <summary>
        /// Unique identifier for this player across all matches.
        /// Used for player tracking, statistics, and reconnection in multiplayer.
        /// </summary>
        public Guid PlayerId { get; private set; }

        /// <summary>
        /// Display name for this player (for UI purposes).
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The faction color this player is using for the current match.
        /// </summary>
        public PlayerColor Color { get; private set; }

        // --- Economy ---
        private int _power;
        private int _influence;

        public int Power
        {
            get => _power;
            internal set => _power = value < 0 ? 0 : value;
        }

        public int Influence
        {
            get => _influence;
            internal set => _influence = value < 0 ? 0 : value;
        }
        public int VictoryPoints { get; internal set; }

        // --- Military ---
        public int TroopsInBarracks { get; internal set; } = GameConstants.STARTING_TROOPS;
        public int SpiesInBarracks { get; internal set; } = GameConstants.STARTING_SPIES;
        public int TrophyHall { get; internal set; } = 0;

        // --- Card Piles ---

        // Encapsulated Deck Manager
        private readonly Deck _deckManager = new Deck();

        // Standard Collections
        internal List<Card> Hand { get; private set; } = new List<Card>();
        internal List<Card> PlayedCards { get; private set; } = new List<Card>();

        // Distinct list for Promoted cards
        internal List<Card> InnerCircle { get; private set; } = new List<Card>();

        // Expose via read-only lists
        internal IReadOnlyList<Card> Deck => _deckManager.DrawPile;
        internal IReadOnlyList<Card> DiscardPile => _deckManager.DiscardPile;

        internal Deck DeckManager => _deckManager; // For Tests/Setup that need write access (e.g. AddToTop)

        /// <summary>
        /// Creates a new player with the specified color and optional identity.
        /// </summary>
        /// <param name="color">The faction color for this player.</param>
        /// <param name="playerId">Optional unique identifier. If null, a new GUID will be generated.</param>
        /// <param name="displayName">Optional display name. If empty, defaults to "Player {color}".</param>
        public Player(PlayerColor color, Guid? playerId = null, string displayName = "")
        {
            PlayerId = playerId ?? Guid.NewGuid();
            Color = color;
            DisplayName = string.IsNullOrEmpty(displayName) ? $"Player {color}" : displayName;
        }



        // --- Deck Management ---

        /// <summary>
        /// Draws the specified number of cards from the deck.
        /// </summary>
        /// <param name="count">Number of cards to draw.</param>
        /// <param name="random">Random number generator for shuffling if needed.</param>
        internal void DrawCards(int count, IGameRandom random)
        {
            var drawn = _deckManager.Draw(count, random);
            foreach (var card in drawn)
            {
                card.Location = CardLocation.Hand;
                Hand.Add(card);
            }
        }

        /// <summary>
        /// Attempts to promote a card from Hand or PlayedCards to the Inner Circle.
        /// </summary>
        /// <param name="card">The card to promote.</param>
        /// <param name="errorMessage">Error message if promotion fails.</param>
        /// <returns>True if promotion succeeded, false otherwise.</returns>
        internal bool TryPromoteCard(Card card, out string errorMessage)
        {
            if (card == null)
            {
                errorMessage = "Card cannot be null";
                return false;
            }

            bool removed = Hand.Remove(card);

            if (!removed)
            {
                removed = PlayedCards.Remove(card);
            }

            if (!removed)
            {
                // Card not found in Hand or PlayedCards
                // Note: Promotion from Discard pile is not currently supported
                errorMessage = $"Card '{card.Name}' not found in Hand or Played area";
                return false;
            }

            card.Location = CardLocation.InnerCircle;
            InnerCircle.Add(card);
            errorMessage = string.Empty;
            return true;
        }

        internal void CleanUpTurn()
        {
            // Move Played Cards to Discard
            _deckManager.AddToDiscard(PlayedCards);
            PlayedCards.Clear();

            // Move Hand to Discard
            _deckManager.AddToDiscard(Hand);
            Hand.Clear();

            Power = 0;
            Influence = 0;
        }
    }
}

