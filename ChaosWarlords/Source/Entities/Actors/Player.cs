using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Source.Entities.Actors
{
    public class Player
    {
        // --- Economy ---
        private int _power;
        private int _influence;

        public PlayerColor Color { get; private set; }

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

        public Player(PlayerColor color)
        {
            Color = color;
        }

        // --- Resource Methods (Refactored) ---

        /// <summary>
        /// Attempts to spend the specified amount of Power.
        /// Returns true if successful, false if insufficient funds.
        /// </summary>
        public bool TrySpendPower(int amount)
        {
            if (Power >= amount)
            {
                Power -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to spend the specified amount of Influence.
        /// Returns true if successful, false if insufficient funds.
        /// </summary>
        public bool TrySpendInfluence(int amount)
        {
            if (Influence >= amount)
            {
                Influence -= amount;
                return true;
            }
            return false;
        }

        // --- Deck Management ---

        public void DrawCards(int count)
        {
            var drawn = _deckManager.Draw(count);
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
        public bool TryPromoteCard(Card card, out string errorMessage)
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

        public void CleanUpTurn()
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

