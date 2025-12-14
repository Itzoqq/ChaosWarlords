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

        [ExcludeFromCodeCoverage] // Exclude the file I/O part from coverage, we test the logic below
        public static void Load(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                LoadFromJson(json);
            }
        }

        // Internal method for testability, allowing us to pass JSON directly
        internal static void LoadFromJson(string json)
        {
            // Case-insensitive matching helps avoid capitalization errors
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _cardDataCache = JsonSerializer.Deserialize<List<CardData>>(json, options);
        }

        // Helper for tests to reset the static state
        internal static void ClearCache() => _cardDataCache = null;

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