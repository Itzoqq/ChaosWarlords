using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class Player
    {
        public PlayerColor Color { get; private set; }

        // --- Economy ---
        // Public Get (UI needs to see it), Internal Set (Only Logic/Tests change it)
        public int Power { get; internal set; }
        public int Influence { get; internal set; }
        public int VictoryPoints { get; internal set; }

        // --- Military ---
        public int TroopsInBarracks { get; internal set; } = 40;
        public int SpiesInBarracks { get; internal set; } = 5;
        public int TrophyHall { get; internal set; } = 0;

        // --- Card Piles ---
        // Internal: Only Game Logic and Tests can touch the lists directly
        internal List<Card> Deck { get; private set; } = new List<Card>();
        internal List<Card> Hand { get; private set; } = new List<Card>();
        internal List<Card> DiscardPile { get; private set; } = new List<Card>();
        internal List<Card> PlayedCards { get; private set; } = new List<Card>();

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