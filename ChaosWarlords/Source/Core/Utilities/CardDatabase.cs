using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
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
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int Cost { get; set; }
        public required string Aspect { get; set; }
        public int DeckVP { get; set; }
        public int InnerCircleVP { get; set; }
        public required List<CardEffectData> Effects { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CardEffectData
    {
        public required string Type { get; set; }
        public int Amount { get; set; }
        public string? TargetResource { get; set; }
        public bool RequiresFocus { get; set; }
        public CardEffectData? OnSuccess { get; set; }

        public string? ConditionType { get; set; }
        public int ConditionThreshold { get; set; }
        public string? ConditionResource { get; set; }

        public bool IsOptional { get; set; }
        public CardEffectData? AlternativeEffect { get; set; }
    }

    public class CardDatabase : ICardDatabase
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private List<CardData> _cardDataCache = [];

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
            _cardDataCache = JsonSerializer.Deserialize<List<CardData>>(json, s_jsonOptions) ?? new List<CardData>();
        }

        public List<Card> GetAllMarketCards(IGameRandom? random = null)
        {
            var cards = new List<Card>();
            if (_cardDataCache is null) return cards;

            foreach (var data in _cardDataCache.OrderBy(c => c.Id))
            {
                // Trace for Replay Desync Debugging
                Console.WriteLine($"[CardDatabase] Processing Market Card: {data.Id}"); 
                cards.Add(CardFactory.CreateFromData(data, random));
            }
            return cards;
        }

        public Card? GetCardById(string id, IGameRandom? random = null)
        {
            var data = _cardDataCache?.FirstOrDefault(c => c.Id == id);
            return data is not null ? CardFactory.CreateFromData(data, random) : null;
        }
    }
}


