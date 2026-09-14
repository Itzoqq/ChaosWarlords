namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// Fail-closed structural validation for a batch of <see cref="CardData"/> about to become
    /// (or be merged into) a live <see cref="CardDatabase"/> catalog. Runs before any card in the
    /// batch is minted into a real <see cref="Entities.Cards.Card"/>: <see cref="CardFactory"/>'s
    /// own enum parsing (e.g. its <c>Enum.TryParse(data.Aspect, ...)</c> call) silently falls back
    /// to that enum's default value on a failed parse rather than throwing, so this is the one
    /// place a malformed field actually gets caught instead of quietly becoming the wrong card.
    /// Throws a single aggregated <see cref="InvalidDataException"/> listing every problem found
    /// across the whole batch, so a catalog author fixes them all in one pass instead of one
    /// throw-fix-reload cycle at a time.
    /// </summary>
    internal static class CardCatalogValidator
    {
        /// <summary>
        /// Validates every card in <paramref name="cards"/> - unique ids, every enum-valued
        /// string field against its real enum type, non-negative <c>MarketCopyCount</c> for
        /// every actual market card, every <see cref="CardEffectData.TargetCardId"/> reference
        /// resolving to a real card, and (only when <paramref name="disallowTestPrefixedIds"/>
        /// is <see langword="true"/>) the absence of any <c>test_</c>-prefixed id. Throws
        /// <see cref="InvalidDataException"/> with every problem found if any exist; a no-op
        /// otherwise. <paramref name="cards"/> is the FULL set the id-reference/uniqueness checks
        /// must see - callers merging a batch into an already-loaded catalog (<see
        /// cref="CardDatabase.LoadAdditionalFromJson"/>) must pass the combined set, not just the
        /// new batch, so a reference spanning both sides resolves correctly.
        /// </summary>
        public static void Validate(IReadOnlyList<CardData> cards, bool disallowTestPrefixedIds)
        {
            var errors = new List<string>();

            ValidateUniqueIds(cards, errors);

            var knownIds = BuildKnownIds(cards);
            foreach (var card in cards)
            {
                ValidateCard(card, disallowTestPrefixedIds, knownIds, errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    "Card catalog validation failed:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
            }
        }

        private static void ValidateUniqueIds(IReadOnlyList<CardData> cards, List<string> errors)
        {
            foreach (var group in cards.GroupBy(c => c.Id))
            {
                if (group.Count() > 1)
                {
                    errors.Add($"Duplicate card id '{group.Key}' appears {group.Count()} times.");
                }
            }
        }

        /// <summary>
        /// Every id a <see cref="CardEffectData.TargetCardId"/> reference may legally point at -
        /// every id in <paramref name="cards"/> plus the 2 hardcoded starting-deck ids
        /// (<c>CardFactory.CreateSoldier</c>/<c>CreateNoble</c>, resolved as synthetic
        /// definitions by <see cref="CardDatabase.GetCardById"/> even though they have no
        /// cards.json entry of their own).
        /// </summary>
        private static HashSet<string> BuildKnownIds(IReadOnlyList<CardData> cards)
        {
            var ids = new HashSet<string>(cards.Select(c => c.Id), StringComparer.Ordinal)
            {
                "soldier",
                "noble"
            };
            return ids;
        }

        // Split into one small, single-purpose method per concern (rather than one long method
        // sequencing every check) specifically to keep cyclomatic complexity per method low - see
        // master.md's risk-hotspot check.
        private static void ValidateCard(CardData card, bool disallowTestPrefixedIds, HashSet<string> knownIds, List<string> errors)
        {
            ValidateTestPrefixedId(card, disallowTestPrefixedIds, errors);
            ValidateCardLevelEnums(card, errors);
            ValidateMarketCopyCount(card, errors);
            ValidateEffectTree(card, knownIds, errors);
        }

        private static void ValidateTestPrefixedId(CardData card, bool disallowTestPrefixedIds, List<string> errors)
        {
            if (!disallowTestPrefixedIds) return;
            if (!card.Id.StartsWith("test_", StringComparison.OrdinalIgnoreCase)) return;

            errors.Add($"Card '{card.Id}': test-prefixed ids may not ship in production data - see TestFixtureCards.");
        }

        private static void ValidateCardLevelEnums(CardData card, List<string> errors)
        {
            RequireEnum<CardAspect>(card.Id, nameof(CardData.Aspect), card.Aspect, required: true, errors);
            RequireEnum<MarketHalfDeck>(card.Id, nameof(CardData.HalfDeck), card.HalfDeck, required: false, errors);
            RequireEnum<CardCreatureType>(card.Id, nameof(CardData.CreatureType), card.CreatureType, required: false, errors);
        }

        // A card that never reaches the market-copy expansion path at all (a fixed recruit pile
        // or Insane Outcast's supply pile) has no meaningful MarketCopyCount - only an actual
        // shuffled-market-eligible card needs one.
        private static void ValidateMarketCopyCount(CardData card, List<string> errors)
        {
            bool isMarketCard = !card.RedirectsToSupplyOnDevourOrPromote && card.FixedRecruitPileSize == 0;
            if (!isMarketCard) return;
            if (card.MarketCopyCount > 0) return;

            errors.Add($"Card '{card.Id}': MarketCopyCount must be at least 1 (was {card.MarketCopyCount}).");
        }

        private static void ValidateEffectTree(CardData card, HashSet<string> knownIds, List<string> errors)
        {
            foreach (var effect in card.Effects ?? [])
            {
                ValidateEffect(card.Id, effect, knownIds, errors);
            }

            if (card.ReactiveDiscardEffect is not null)
            {
                ValidateEffect(card.Id, card.ReactiveDiscardEffect, knownIds, errors);
            }
        }

        private static void ValidateEffect(string cardId, CardEffectData effect, HashSet<string> knownIds, List<string> errors)
        {
            ValidateEffectEnums(cardId, effect, errors);
            ValidateTargetCardIdReference(cardId, effect, knownIds, errors);
            ValidateNestedEffects(cardId, effect, knownIds, errors);
        }

        private static void ValidateEffectEnums(string cardId, CardEffectData effect, List<string> errors)
        {
            RequireEnum<EffectType>(cardId, nameof(CardEffectData.Type), effect.Type, required: true, errors);
            RequireEnum<ResourceType>(cardId, nameof(CardEffectData.TargetResource), effect.TargetResource, required: false, errors);
            RequireEnum<CardLocation>(cardId, nameof(CardEffectData.TargetLocation), effect.TargetLocation, required: false, errors);
            RequireEnum<ConditionType>(cardId, nameof(CardEffectData.ConditionType), effect.ConditionType, required: false, errors);
            RequireEnum<ResourceType>(cardId, nameof(CardEffectData.ConditionResource), effect.ConditionResource, required: false, errors);
            RequireEnum<SitePresenceType>(cardId, nameof(CardEffectData.ConditionPresenceType), effect.ConditionPresenceType, required: false, errors);
            RequireEnum<DynamicAmountSource>(cardId, nameof(CardEffectData.DynamicAmountSource), effect.DynamicAmountSource, required: false, errors);
            RequireEnum<CardAspect>(cardId, nameof(CardEffectData.RequiredPromotionAspect), effect.RequiredPromotionAspect, required: false, errors);
            RequireEnum<CardCreatureType>(cardId, nameof(CardEffectData.RequiredPromotionCreatureType), effect.RequiredPromotionCreatureType, required: false, errors);
            RequireEnum<ResourceType>(cardId, nameof(CardEffectData.GainResourcePerRepeat), effect.GainResourcePerRepeat, required: false, errors);
        }

        private static void ValidateTargetCardIdReference(string cardId, CardEffectData effect, HashSet<string> knownIds, List<string> errors)
        {
            if (string.IsNullOrEmpty(effect.TargetCardId)) return;
            if (knownIds.Contains(effect.TargetCardId)) return;

            errors.Add($"Card '{cardId}': TargetCardId '{effect.TargetCardId}' does not reference any known card.");
        }

        // Always recurses into a node's children, even when the node's OWN Type failed to parse
        // above - unlike CardFactory.CreateEffect, which returns null immediately on a bad Type
        // and never even looks at that node's children. Deliberate: this validator's job is
        // surfacing every real problem in one pass (see this class's own doc comment), and a
        // typo'd Type alongside an unrelated typo further down the same chain are 2 separate
        // things a catalog author needs to fix - reporting only the first would just mean a
        // second throw-fix-reload cycle to find the other. Harmless either way for a batch that's
        // actually valid once fixed.
        private static void ValidateNestedEffects(string cardId, CardEffectData effect, HashSet<string> knownIds, List<string> errors)
        {
            if (effect.OnSuccess is not null) ValidateEffect(cardId, effect.OnSuccess, knownIds, errors);
            if (effect.Alternative is not null) ValidateEffect(cardId, effect.Alternative, knownIds, errors);
            if (effect.PromotionCompletionEffect is not null) ValidateEffect(cardId, effect.PromotionCompletionEffect, knownIds, errors);
        }

        private static void RequireEnum<TEnum>(string cardId, string fieldName, string? rawValue, bool required, List<string> errors)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrEmpty(rawValue))
            {
                if (required)
                {
                    errors.Add($"Card '{cardId}': {fieldName} is required but missing/empty.");
                }
                return;
            }

            if (!Enum.TryParse<TEnum>(rawValue, ignoreCase: true, out _))
            {
                errors.Add($"Card '{cardId}': {fieldName} '{rawValue}' is not a known {typeof(TEnum).Name} value.");
            }
        }
    }
}
