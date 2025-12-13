using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using ChaosWarlords.Source.Entities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Utilities
{
    [ExcludeFromCodeCoverage]
    public class CardData { public string Id { get; set; } public string Name { get; set; } public int Cost { get; set; } public string Aspect { get; set; } public int VictoryPoints { get; set; } public string Text { get; set; } }

    public static class CardDatabase
    {
        private static List<CardData> _cardDataCache;

        // Removed Texture2D
        public static void Load(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return;
            string json = File.ReadAllText(jsonPath);
            _cardDataCache = JsonSerializer.Deserialize<List<CardData>>(json);
        }

        public static List<Card> GetAllMarketCards()
        {
            var cards = new List<Card>();
            if (_cardDataCache == null) return cards;

            foreach (var data in _cardDataCache)
            {
                // Removed Texture2D
                cards.Add(CardFactory.CreateFromData(data));
            }
            return cards;
        }
    }
}