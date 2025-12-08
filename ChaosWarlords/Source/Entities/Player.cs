using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Player
    {
        public PlayerColor Color { get; private set; }
        
        // --- Economy ---
        public int Power { get; set; }
        public int Influence { get; set; }
        public int VictoryPoints { get; set; }

        // --- Card Piles ---
        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> DiscardPile { get; private set; } = new List<Card>();
        public List<Card> PlayedCards { get; private set; } = new List<Card>(); // Cards currently on the table this turn

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
                    if (Deck.Count == 0) break; // Still empty? Stop.
                }

                Card card = Deck[0];
                Deck.RemoveAt(0);
                card.Location = CardLocation.Hand;
                Hand.Add(card);
            }
        }

        private void ReshuffleDiscard()
        {
            // Simple shuffle logic would go here
            // For now, just move discard back to deck
            Deck.AddRange(DiscardPile);
            DiscardPile.Clear();
        }
        
        public void CleanUpTurn()
        {
            // Move played cards and hand to discard
            DiscardPile.AddRange(PlayedCards);
            PlayedCards.Clear();
            
            DiscardPile.AddRange(Hand);
            Hand.Clear();
            
            // Reset pools
            Power = 0;
            Influence = 0;
        }
    }
}