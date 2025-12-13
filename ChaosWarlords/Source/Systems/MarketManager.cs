using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MarketManager
    {
        internal List<Card> MarketDeck { get; private set; } = new List<Card>();
        internal List<Card> MarketRow { get; private set; } = new List<Card>();

        // Positions for the 6 market slots
        private List<Vector2> _slotPositions = new List<Vector2>();

        public MarketManager()
        {
            // Define where the 6 cards sit on screen (e.g., Top Middle)
            for (int i = 0; i < 6; i++)
            {
                _slotPositions.Add(new Vector2(300 + (i * 160), 50));
            }
        }

        public void InitializeDeck(List<Card> allCards)
        {
            MarketDeck = allCards;
            // Shuffle logic here later...
            RefillMarket();
        }

        public void RefillMarket()
        {
            while (MarketRow.Count < 6 && MarketDeck.Count > 0)
            {
                Card newCard = MarketDeck[0];
                MarketDeck.RemoveAt(0);
                newCard.Location = CardLocation.Market;
                MarketRow.Add(newCard);
            }

            // Update positions
            for (int i = 0; i < MarketRow.Count; i++)
            {
                MarketRow[i].Position = _slotPositions[i];
            }
        }

        public void Update(MouseState mouseState, Player activePlayer)
        {
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;

            // Iterate backwards to safely remove
            for (int i = MarketRow.Count - 1; i >= 0; i--)
            {
                var card = MarketRow[i];
                card.Update(null, mouseState);

                if (card.IsHovered && isClicking)
                {
                    // DELEGATE to the new testable method
                    TryBuyCard(activePlayer, card);
                    return; // Prevent multi-buy
                }
            }
        }

        private void BuyCard(Player player, Card card)
        {
            player.Influence -= card.Cost;
            player.DiscardPile.Add(card);
            MarketRow.Remove(card);

            GameLogger.Log($"Bought {card.Name} for {card.Cost} Influence.", LogChannel.Economy);

            // Immediately refill? Or wait for end of turn? 
            // Tyrants rules: "Immediately replace it" (Rulebook pg 12 "Recruit a Card")
            RefillMarket();
        }

        public bool TryBuyCard(Player player, Card card)
        {
            if (player.Influence >= card.Cost)
            {
                player.Influence -= card.Cost;
                player.DiscardPile.Add(card);
                MarketRow.Remove(card);

                // Use simple null check for Logger so tests don't crash if Logger isn't initialized
                try { GameLogger.Log($"Bought {card.Name}", LogChannel.Economy); } catch { }

                RefillMarket();
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            foreach (var card in MarketRow)
            {
                card.Draw(spriteBatch, font);
            }
        }
    }
}