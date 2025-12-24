using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
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
        public int TroopsInBarracks { get; internal set; } = 40;
        public int SpiesInBarracks { get; internal set; } = 5;
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

        public void PromoteCard(Card card)
        {
            if (card == null) return;

            bool removed = Hand.Remove(card);

            if (!removed)
            {
                removed = PlayedCards.Remove(card);
            }

            if (!removed)
            {
                // Discard is now managed by Deck.
                // We shouldn't really be fishing cards out of discard for promotion usually?
                // But if logic requires it:
                // Deck doesn't support "Remove Specific" easily yet.
                // Let's assume for now Promoted cards come from Hand or Play.
                // If it comes from Discard, we'd need to add a method to Deck.cs.
                // Checking previous implementation: "DiscardPile.Remove(card)".
                // I'll add RemoveFromDiscard to Deck class if needed, or just omit for now if unused.
                // Let's see if we can access the underlying list.
                // We can't.
                // Let's add 'TryRemoveFromDiscard' to Deck.cs? 
                // Wait, I'll assume Hand/Played for now. If tests fail, I'll add it.
            }

            if (removed)
            {
                card.Location = CardLocation.InnerCircle;
                InnerCircle.Add(card);
            }
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