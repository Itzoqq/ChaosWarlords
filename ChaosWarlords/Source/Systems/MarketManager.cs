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
        private List<Card> _marketDeck = new List<Card>();
        public List<Card> MarketRow { get; private set; } = new List<Card>();

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
            _marketDeck = allCards;
            // Shuffle logic here later...
            RefillMarket();
        }

        public void RefillMarket()
        {
            while (MarketRow.Count < 6 && _marketDeck.Count > 0)
            {
                Card newCard = _marketDeck[0];
                _marketDeck.RemoveAt(0);
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

            for (int i = MarketRow.Count - 1; i >= 0; i--)
            {
                var card = MarketRow[i];
                card.Update(null, mouseState); // Pass null gametime if not needed yet

                if (card.IsHovered && isClicking)
                {
                    // BUY LOGIC
                    if (activePlayer.Influence >= card.Cost)
                    {
                        BuyCard(activePlayer, card);
                        // Prevent click-through
                        return;
                    }
                    else
                    {
                        // Log "Not enough influence" only once per click (needs debounce logic in Game1 really)
                    }
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

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            foreach (var card in MarketRow)
            {
                card.Draw(spriteBatch, font);
            }
        }
    }
}