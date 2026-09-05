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
        public string? ConditionPresenceType { get; set; }

        public bool IsOptional { get; set; }

        public bool ReplaceWithSource { get; set; }

        // "Assassinate/Supplant a white troop" - see CardEffect.TargetNeutralTroopOnly.
        public bool TargetNeutralTroopOnly { get; set; }

        // "Supplant even without Presence at the site" - see CardEffect.IgnoresPresenceRequirement.
        public bool IgnoresPresenceRequirement { get; set; }

        // "Gain 1 VP for every 2 sites you control" - see CardEffect.DynamicAmountSource.
        public string? DynamicAmountSource { get; set; }
        public int DynamicAmountDivisor { get; set; } = 1;
    }

    public class CardDatabase : ICardDatabase
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ILocalizationService _localization;
        private readonly IGameLogger? _logger;
        private List<CardData> _cardDataCache = [];

        public CardDatabase(ILocalizationService localization, IGameLogger? logger = null)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _logger = logger;
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
                _logger?.Log($"[CardDatabase] Processing Market Card: {data.Id}", LogChannel.Debug);
                cards.Add(CardFactory.CreateFromData(data, _localization, random, _logger));
            }
            return cards;
        }

        /// <summary>
        /// Resolves a card by its definitional/catalog id (Card.DefinitionId - NOT the
        /// per-instance-suffixed Card.Id CardFactory.GenerateUniqueId produces). "soldier"/
        /// "noble" are handled as synthetic definitions here even though they're not in
        /// cards.json - they're the two hardcoded starting-deck cards (CardFactory.
        /// CreateSoldier/CreateNoble), and this is the single lookup every restore path
        /// (StateRestorer) goes through, so it needs to be able to resolve them too.
        /// </summary>
        public Card? GetCardById(string id, IGameRandom? random = null)
        {
            if (id == "soldier") return CardFactory.CreateSoldier(random);
            if (id == "noble") return CardFactory.CreateNoble(random);

            var data = _cardDataCache?.FirstOrDefault(c => c.Id == id);
            return data is not null ? CardFactory.CreateFromData(data, _localization, random, _logger) : null;
        }
    }
}


