using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using ChaosWarlords.Source.Entities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Utilities
{
    // 1. Updated Data Structures to match cards.json
    [ExcludeFromCodeCoverage]
    public class CardData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } // Was 'Text', fixed to match JSON
        public int Cost { get; set; }
        public string Aspect { get; set; }
        public int DeckVP { get; set; }        // Was 'VictoryPoints', fixed to match JSON
        public int InnerCircleVP { get; set; }
        public List<CardEffectData> Effects { get; set; } // Added to capture the JSON array
    }

    [ExcludeFromCodeCoverage]
    public class CardEffectData
    {
        public string Type { get; set; }
        public int Amount { get; set; }
        public string TargetResource { get; set; }
    }

    public static class CardDatabase
    {
        private static List<CardData> _cardDataCache;

        public static void Load(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return;
            string json = File.ReadAllText(jsonPath);
            // Case-insensitive matching helps avoid capitalization errors
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _cardDataCache = JsonSerializer.Deserialize<List<CardData>>(json, options);
        }

        public static List<Card> GetAllMarketCards()
        {
            var cards = new List<Card>();
            if (_cardDataCache == null) return cards;

            foreach (var data in _cardDataCache)
            {
                cards.Add(CardFactory.CreateFromData(data));
            }
            return cards;
        }
    }
}