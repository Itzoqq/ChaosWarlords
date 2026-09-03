using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Source.Utilities
{
    public static class CardFactory
    {
        private static string GenerateUniqueId(string baseId, IGameRandom? random = null)
        {
            if (random != null)
            {
                return $"{baseId}_{random.NextInt(1000000).ToString("x6", System.Globalization.CultureInfo.InvariantCulture)}";
            }
            return $"{baseId}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
        }

        public static Card CreateSoldier(IGameRandom? random = null)
        {
            var card = new Card(GenerateUniqueId("soldier", random), "Soldier", 0, CardAspect.Neutral, 0, 0, 0, definitionId: "soldier");
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));
            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble(IGameRandom? random = null)
        {
            var card = new Card(GenerateUniqueId("noble", random), "Noble", 0, CardAspect.Neutral, 0, 0, 0, definitionId: "noble");
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            card.Description = "+1 Influence";
            return card;
        }

        public static Card CreateFromData(CardData data, ILocalizationService localization, IGameRandom? random = null, IGameLogger? logger = null)
        {
            Enum.TryParse(data.Aspect, true, out CardAspect aspect);

            // Name/Description are resolved from the localization bundle, keyed off the
            // card's definitional Id (NOT the randomized runtime Card.Id generated below) -
            // "{Id}_name"/"{Id}_description". See CardDatabase's CardData doc comment.
            string name = localization.GetString($"{data.Id}_name");

            // Using 0 for influence as default
            var card = new Card(GenerateUniqueId(data.Id, random), name, data.Cost, aspect, data.DeckVP, data.InnerCircleVP, 0, definitionId: data.Id);

            card.Description = localization.GetString($"{data.Id}_description");
            card.RedirectsToSupplyOnDevourOrPromote = data.RedirectsToSupplyOnDevourOrPromote;

            if (data.Effects is not null)
            {
                foreach (var effectData in data.Effects)
                {
                    var effect = CreateEffect(effectData, logger);
                    if (effect != null)
                    {
                        card.AddEffect(effect);
                    }
                }
            }
            return card;
        }

        private static CardEffect? CreateEffect(CardEffectData data, IGameLogger? logger)
        {
            if (!Enum.TryParse(data.Type, true, out EffectType type))
                return null;

            var effect = CreateBaseEffect(data, type);
            ParseTargetLocation(data, effect, logger);
            ParseRecursiveEffect(data, effect, logger);
            ParseCondition(data, effect);
            ParseOptionalFlags(data, effect);

            return effect;
        }

        private static CardEffect CreateBaseEffect(CardEffectData data, EffectType type)
        {
            ResourceType resType = ResourceType.None;
            if (!string.IsNullOrEmpty(data.TargetResource))
            {
                Enum.TryParse(data.TargetResource, true, out resType);
            }

            var effect = new CardEffect(type, data.Amount, resType);
            effect.RequiresFocus = data.RequiresFocus;
            return effect;
        }

        private static void ParseTargetLocation(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (string.IsNullOrEmpty(data.TargetLocation))
                return;

            if (Enum.TryParse(data.TargetLocation, true, out CardLocation targetLoc))
            {
                effect.TargetLocation = targetLoc;
            }
            else
            {
                logger?.Log($"[CardFactory] FAILED to parse TargetLocation: {data.TargetLocation}", LogChannel.Warning);
            }
        }

        private static void ParseRecursiveEffect(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (data.OnSuccess != null)
            {
                effect.OnSuccess = CreateEffect(data.OnSuccess, logger);
            }

            if (data.Alternative != null)
            {
                effect.Alternative = CreateEffect(data.Alternative, logger);
            }
        }

        private static void ParseCondition(CardEffectData data, CardEffect effect)
        {
            if (string.IsNullOrEmpty(data.ConditionType))
                return;

            if (!Enum.TryParse(data.ConditionType, true, out ConditionType condType))
                return;

            ResourceType condRes = ResourceType.None;
            if (!string.IsNullOrEmpty(data.ConditionResource))
            {
                Enum.TryParse(data.ConditionResource, true, out condRes);
            }

            SitePresenceType? condPresenceType = null;
            if (!string.IsNullOrEmpty(data.ConditionPresenceType) &&
                Enum.TryParse(data.ConditionPresenceType, true, out SitePresenceType parsedPresenceType))
            {
                condPresenceType = parsedPresenceType;
            }

            effect.Condition = new EffectCondition(condType, data.ConditionThreshold, condRes, condPresenceType);
        }

        private static void ParseOptionalFlags(CardEffectData data, CardEffect effect)
        {
            effect.IsOptional = data.IsOptional;
            effect.ReplaceWithSource = data.ReplaceWithSource;
            effect.TargetNeutralTroopOnly = data.TargetNeutralTroopOnly;
            effect.IgnoresPresenceRequirement = data.IgnoresPresenceRequirement;
        }
    }
}


