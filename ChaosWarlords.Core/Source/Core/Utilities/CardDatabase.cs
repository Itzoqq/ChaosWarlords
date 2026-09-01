using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Text.Json;
using ChaosWarlords.Source.Entities.Cards;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Utilities
{
    // 1. Data Structures to match cards.json
    // Name/Description are NOT here - they live in Content/data/localization/en_US.json,
    // resolved via ILocalizationService at card-creation time (CardFactory.CreateFromData),
    // keyed off Id ("{Id}_name"/"{Id}_description"). See planning.txt's localization design.
    [ExcludeFromCodeCoverage]
    public class CardData
    {
        public required string Id { get; set; }
        public int Cost { get; set; }
        public required string Aspect { get; set; }
        public int DeckVP { get; set; }
        public int InnerCircleVP { get; set; }
        public bool RedirectsToSupplyOnDevourOrPromote { get; set; }
        public required List<CardEffectData> Effects { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CardEffectData
    {
        public required string Type { get; set; }
        public int Amount { get; set; }
        public string? TargetResource { get; set; }
        public string? TargetLocation { get; set; } // Where to target (Market, Deck, Hand, etc.)
        public bool RequiresFocus { get; set; }
        public CardEffectData? OnSuccess { get; set; }
        public CardEffectData? Alternative { get; set; }

        public string? ConditionType { get; set; }
        public int ConditionThreshold { get; set; }
        public string? ConditionResource { get; set; }

        public bool IsOptional { get; set; }

        public bool ReplaceWithSource { get; set; }
    }

    public class CardDatabase : ICardDatabase
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ILocalizationService _localization;
        private List<CardData> _cardDataCache = [];

        public CardDatabase(ILocalizationService localization)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

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
                // Supply-pile cards (e.g. Insane Outcast) are never purchasable from the
                // market - they only ever reach a player via another card's effect. Excluded
                // here rather than via a second flag, since RedirectsToSupplyOnDevourOrPromote
                // is otherwise unique to exactly this kind of card.
                if (data.RedirectsToSupplyOnDevourOrPromote)
                {
                    continue;
                }

                // Trace for Replay Desync Debugging
                Console.WriteLine($"[CardDatabase] Processing Market Card: {data.Id}");
                cards.Add(CardFactory.CreateFromData(data, _localization, random));
            }
            return cards;
        }

        public Card? GetCardById(string id, IGameRandom? random = null)
        {
            var data = _cardDataCache?.FirstOrDefault(c => c.Id == id);
            return data is not null ? CardFactory.CreateFromData(data, _localization, random) : null;
        }
    }
}


