using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    /// <summary>
    /// Represents a card in the game, including its stats, cost, and effects.
    /// Cards can flyweight-like definitions or instantiated objects in a player's deck.
    /// </summary>
    public class Card
    {
        // --- Core Data Only ---

        /// <summary>
        /// Unique identifier for the card definition (e.g. "c_obsidian_golem").
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// The localized name of the card.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The influence cost required to purchase this card from the market.
        /// </summary>
        public int Cost { get; private set; }

        /// <summary>
        /// The elemental or factional aspect of the card (e.g. Shadow, Undead).
        /// </summary>
        public CardAspect Aspect { get; private set; }

        /// <summary>
        /// Victory Points worth when in the deck at end of game.
        /// </summary>
        public int DeckVP { get; private set; }

        /// <summary>
        /// Victory Points worth when promoted to the Inner Circle.
        /// </summary>
        public int InnerCircleVP { get; private set; }

        /// <summary>
        /// Influence generated when this card is played.
        /// </summary>
        public int InfluenceValue { get; private set; }

        /// <summary>
        /// List of special effects triggered when the card is played.
        /// </summary>
        public List<CardEffect> Effects { get; private set; } = [];

        /// <summary>
        /// User-visible description of what the card does.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Current location of this card instance (Deck, Hand, Discard, etc.).
        /// </summary>
        public CardLocation Location { get; set; }

        // Constants moved to GameConstants.CardRendering for centralization
        public static int Width => Utilities.GameConstants.CardRendering.CardWidth;
        public static int Height => Utilities.GameConstants.CardRendering.CardHeight;

        public Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerCircleVp, int influence)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Aspect = aspect;
            DeckVP = deckVp;
            InnerCircleVP = innerCircleVp;
            InfluenceValue = influence;
        }

        public void AddEffect(CardEffect effect)
        {
            Effects.Add(effect);
        }

        public Card Clone()
        {
            var newCard = new Card(Id, Name, Cost, Aspect, DeckVP, InnerCircleVP, InfluenceValue)
            {
                Description = Description,
                Location = Location
                // REMOVED: Position = Position, IsHovered = IsHovered
            };

            foreach (var effect in Effects)
            {
                newCard.Effects.Add(new CardEffect(effect.Type, effect.Amount, effect.TargetResource));
            }

            return newCard;
        }
    }
}

