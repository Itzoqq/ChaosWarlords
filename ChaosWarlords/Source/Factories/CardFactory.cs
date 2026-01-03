using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using System;

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
            var card = new Card(GenerateUniqueId("soldier", random), "Soldier", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));
            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble(IGameRandom? random = null)
        {
            var card = new Card(GenerateUniqueId("noble", random), "Noble", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            card.Description = "+1 Influence";
            return card;
        }

        public static Card CreateFromData(CardData data, IGameRandom? random = null)
        {
            Enum.TryParse(data.Aspect, true, out CardAspect aspect);

            // Using 0 for influence as default
            var card = new Card(GenerateUniqueId(data.Id, random), data.Name, data.Cost, aspect, data.DeckVP, data.InnerCircleVP, 0);

            card.Description = data.Description;

            if (data.Effects is not null)
            {
                foreach (var effectData in data.Effects)
                {
                    var effect = CreateEffect(effectData);
                    if (effect != null)
                    {
                        card.AddEffect(effect);
                    }
                }
            }
            return card;
        }

        private static CardEffect? CreateEffect(CardEffectData data)
        {
            if (Enum.TryParse(data.Type, true, out EffectType type))
            {
                ResourceType resType = ResourceType.None;
                if (!string.IsNullOrEmpty(data.TargetResource))
                {
                    Enum.TryParse(data.TargetResource, true, out resType);
                }

                var effect = new CardEffect(type, data.Amount, resType);
                effect.RequiresFocus = data.RequiresFocus;

                // Recursive creation
                if (data.OnSuccess != null)
                {
                    effect.OnSuccess = CreateEffect(data.OnSuccess);
                }

                // Conditional Logic
                if (!string.IsNullOrEmpty(data.ConditionType) && Enum.TryParse(data.ConditionType, true, out ConditionType condType))
                {
                    ResourceType condRes = ResourceType.None;
                    if (!string.IsNullOrEmpty(data.ConditionResource))
                    {
                        Enum.TryParse(data.ConditionResource, true, out condRes);
                    }
                    effect.Condition = new EffectCondition(condType, data.ConditionThreshold, condRes);
                }

                effect.IsOptional = data.IsOptional;

                return effect;
            }
            return null;
        }
    }
}


