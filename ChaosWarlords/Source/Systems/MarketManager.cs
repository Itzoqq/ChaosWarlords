using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Systems
{
    public class MarketManager
    {
        public List<Card> MarketDeck { get; private set; } = new List<Card>();
        public List<Card> MarketRow { get; private set; } = new List<Card>();

        public void InitializeDeck(List<Card> allCards)
        {
            MarketDeck = allCards; // In real game, Shuffle here
            RefillMarket();
        }

        public void RefillMarket()
        {
            while (MarketRow.Count < 6 && MarketDeck.Count > 0)
            {
                var card = MarketDeck[0];
                MarketDeck.RemoveAt(0);
                MarketRow.Add(card);
            }
            ArrangeMarket();
        }

        private void ArrangeMarket()
        {
            int startX = 100;
            int y = 50;
            int gap = 160;

            for (int i = 0; i < MarketRow.Count; i++)
            {
                MarketRow[i].Position = new Vector2(startX + (i * gap), y);
            }
        }

        public bool TryBuyCard(Player player, Card card)
        {
            if (card == null) return false;
            if (!MarketRow.Contains(card)) return false;
            if (player.Influence < card.Cost) return false;

            player.Influence -= card.Cost;
            MarketRow.Remove(card);
            card.Location = CardLocation.DiscardPile; // Update location
            player.DiscardPile.Add(card);

            RefillMarket(); // Immediately refill logic
            return true;
        }

        public void Update(MouseState mouse, Player player)
        {
            Point mousePoint = new Point(mouse.X, mouse.Y);
            foreach (var card in MarketRow)
            {
                card.IsHovered = card.Bounds.Contains(mousePoint);
                if (card.IsHovered && mouse.LeftButton == ButtonState.Pressed && player.Influence >= card.Cost)
                {
                    // Buy Logic would go here (Triggered by UI click usually)
                }
            }
        }

        // Removed Draw() - The GameplayState will handle drawing the market using CardRenderer
    }
}