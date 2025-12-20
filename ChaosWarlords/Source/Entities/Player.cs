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
        internal List<Card> Deck { get; private set; } = new List<Card>();
        internal List<Card> Hand { get; private set; } = new List<Card>();
        internal List<Card> DiscardPile { get; private set; } = new List<Card>();
        internal List<Card> PlayedCards { get; private set; } = new List<Card>();

        // Distinct list for Promoted cards
        internal List<Card> InnerCircle { get; private set; } = new List<Card>();

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
            for (int i = 0; i < count; i++)
            {
                if (Deck.Count == 0)
                {
                    ReshuffleDiscard();
                    if (Deck.Count == 0) break;
                }

                Card card = Deck[0];
                Deck.RemoveAt(0);
                card.Location = CardLocation.Hand;
                Hand.Add(card);
            }
        }

        private void ReshuffleDiscard()
        {
            Deck.AddRange(DiscardPile);
            DiscardPile.Clear();
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
                removed = DiscardPile.Remove(card);
            }

            if (removed)
            {
                card.Location = CardLocation.InnerCircle;
                InnerCircle.Add(card);
            }
        }

        public void CleanUpTurn()
        {
            DiscardPile.AddRange(PlayedCards);
            PlayedCards.Clear();

            DiscardPile.AddRange(Hand);
            Hand.Clear();

            Power = 0;
            Influence = 0;
        }
    }
}