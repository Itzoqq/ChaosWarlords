using ChaosWarlords.Source.Core.Interfaces.Data;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using ChaosWarlords.Source.Entities.Cards;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Utilities
{
    // 1. Data Structures to match cards.json
    [ExcludeFromCodeCoverage]
    public class CardData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
        public string Aspect { get; set; }
        public int DeckVP { get; set; }
        public int InnerCircleVP { get; set; }
        public List<CardEffectData> Effects { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CardEffectData
    {
        public string Type { get; set; }
        public int Amount { get; set; }
        public string TargetResource { get; set; }
        public bool RequiresFocus { get; set; }
    }

    public class CardDatabase : ICardDatabase
    {
        private List<CardData> _cardDataCache;

        public void Load(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                LoadFromJson(json);
            }
        }

        internal void LoadFromJson(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _cardDataCache = JsonSerializer.Deserialize<List<CardData>>(json, options);
        }

        public List<Card> GetAllMarketCards()
        {
            var cards = new List<Card>();
            if (_cardDataCache == null) return cards;

            foreach (var data in _cardDataCache)
            {
                cards.Add(CardFactory.CreateFromData(data));
            }
            return cards;
        }

        public Card GetCardById(string id)
        {
            var data = _cardDataCache?.FirstOrDefault(c => c.Id == id);
            return data != null ? CardFactory.CreateFromData(data) : null;
        }
    }
}


