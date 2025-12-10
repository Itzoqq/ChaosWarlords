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

        // --- Military ---
        public int TroopsInBarracks { get; set; } = 40; // Starting limit per rules
        public int SpiesInBarracks { get; set; } = 5; // Starting limit per rules
        public int TrophyHall { get; set; } = 0; // Count of enemy troops assassinated

        // --- Card Piles ---
        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> DiscardPile { get; private set; } = new List<Card>();
        public List<Card> PlayedCards { get; private set; } = new List<Card>();

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
            // Simple shuffle logic (In real game, randomize this list!)
            Deck.AddRange(DiscardPile);
            DiscardPile.Clear();
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