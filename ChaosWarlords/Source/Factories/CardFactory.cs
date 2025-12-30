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
                    if (Enum.TryParse(effectData.Type, true, out EffectType type))
                    {
                        ResourceType resType = ResourceType.None;
                        if (!string.IsNullOrEmpty(effectData.TargetResource))
                        {
                            Enum.TryParse(effectData.TargetResource, true, out resType);
                        }

                        // --- CHANGED: Explicitly set RequiresFocus ---
                        var newEffect = new CardEffect(type, effectData.Amount, resType);
                        newEffect.RequiresFocus = effectData.RequiresFocus;

                        card.AddEffect(newEffect);
                    }
                }
            }
            return card;
        }
    }
}


