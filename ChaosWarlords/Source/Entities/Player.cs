using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Player
    {
        // --- Economy ---
        // Backing fields
        private int _power;
        private int _influence;

        public PlayerColor Color { get; private set; }

        // Logic: Clamp to 0 to prevent bugs propagating
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

        // NEW: Distinct list for Promoted cards
        internal List<Card> InnerCircle { get; private set; } = new List<Card>();

        public Player(PlayerColor color)
        {
            Color = color;
        }

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

        /// <summary>
        /// Moves a card from its current active location (Hand or Played) to the Inner Circle.
        /// This effectively removes it from the deck cycle for the rest of the game.
        /// </summary>
        /// <param name="card">The card to promote.</param>
        public void PromoteCard(Card card)
        {
            if (card == null) return;

            // 1. Attempt to remove the card from valid active zones.
            // A card might be promoted from Hand (via an effect) or from PlayedCards (standard Promote action).
            bool removed = Hand.Remove(card);

            if (!removed)
            {
                removed = PlayedCards.Remove(card);
            }

            // Edge case: Some rare effects might promote from Discard, though rare in core rules.
            if (!removed)
            {
                removed = DiscardPile.Remove(card);
            }

            // 2. If successfully found and removed, move to Inner Circle.
            if (removed)
            {
                card.Location = CardLocation.InnerCircle;
                InnerCircle.Add(card);
            }
        }

        public void CleanUpTurn()
        {
            // Because PromoteCard() physically removes the item from Hand/PlayedCards,
            // we don't need to change logic here. The lists below will only contain 
            // cards that were NOT promoted.
            DiscardPile.AddRange(PlayedCards);
            PlayedCards.Clear();

            DiscardPile.AddRange(Hand);
            Hand.Clear();

            Power = 0;
            Influence = 0;
        }
    }
}