using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChaosWarlords.Source.Entities;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.Utilities
{
    // A POCO (Plain Old C# Object) just for reading the JSON
    public class CardData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
        public string Aspect { get; set; } // Read as string, parse to Enum
        public int DeckVP { get; set; }
        public int InnerCircleVP { get; set; }
        public List<CardEffectData> Effects { get; set; }
    }

    public class CardEffectData
    {
        public string Type { get; set; }
        public int Amount { get; set; }
        public string TargetResource { get; set; }
    }

    public static class CardDatabase
    {
        private static Dictionary<string, CardData> _definitions = new Dictionary<string, CardData>();
        private static Texture2D _defaultTexture;

        public static void Load(string filePath, Texture2D defaultTexture)
        {
            _defaultTexture = defaultTexture;

            try
            {
                string jsonString = File.ReadAllText(filePath);
                var cards = JsonSerializer.Deserialize<List<CardData>>(jsonString);

                foreach (var c in cards)
                {
                    _definitions[c.Id] = c;
                }
                GameLogger.Log($"Loaded {_definitions.Count} card definitions.", LogChannel.General);
            }
            catch (Exception ex)
            {
                GameLogger.Log(ex);
            }
        }

        public static Card CreateCard(string cardId)
        {
            if (!_definitions.ContainsKey(cardId))
            {
                GameLogger.Log($"Card ID not found: {cardId}", LogChannel.Error);
                return null;
            }

            var data = _definitions[cardId];

            // Parse Enums
            Enum.TryParse(data.Aspect, out CardAspect aspect);

            var card = new Card(data.Id, data.Name, data.Cost, aspect, data.DeckVP, data.InnerCircleVP);
            card.Description = data.Description;
            card.SetTexture(_defaultTexture); // In future, load specific texture by ID

            foreach (var effData in data.Effects)
            {
                Enum.TryParse(effData.Type, out EffectType eType);
                Enum.TryParse(effData.TargetResource, out ResourceType rType);
                card.AddEffect(new CardEffect(eType, effData.Amount, rType));
            }

            return card;
        }

        public static List<Card> GetAllMarketCards()
        {
            // For now, return a list of all loaded cards to populate the market deck
            List<Card> allCards = new List<Card>();
            foreach (var key in _definitions.Keys)
            {
                // In a real game, you'd check if the card belongs to the "Market Deck" vs "Starter Deck"
                if (!key.StartsWith("starter"))
                    allCards.Add(CreateCard(key));
            }
            return allCards;
        }
    }
}