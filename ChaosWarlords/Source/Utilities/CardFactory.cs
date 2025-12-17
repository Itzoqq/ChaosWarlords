using ChaosWarlords.Source.Entities;
using System.Collections.Generic;
using System;

namespace ChaosWarlords.Source.Utilities
{
    public static class CardFactory
    {
        // Helper to ensure unique IDs for every card instance
        private static string GenerateUniqueId(string baseId)
        {
            // Appends a short GUID snippet to make "soldier" -> "soldier_a1b2"
            return $"{baseId}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
        }

        public static Card CreateSoldier()
        {
            // OLD: var card = new Card("soldier", ...);
            // NEW: Use Unique ID
            var card = new Card(GenerateUniqueId("soldier"), "Soldier", 0, CardAspect.Neutral, 0, 0);

            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));
            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble()
        {
            var card = new Card(GenerateUniqueId("noble"), "Noble", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            card.Description = "+1 Influence";
            return card;
        }

        public static Card CreateFromData(CardData data)
        {
            Enum.TryParse(data.Aspect, true, out CardAspect aspect);

            // Even data-driven cards should have unique instance IDs in runtime
            var card = new Card(GenerateUniqueId(data.Id), data.Name, data.Cost, aspect, data.DeckVP, 0);
            card.Description = data.Description;

            if (data.Effects != null)
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
                        card.AddEffect(new CardEffect(type, effectData.Amount, resType));
                    }
                }
            }
            return card;
        }
    }
}