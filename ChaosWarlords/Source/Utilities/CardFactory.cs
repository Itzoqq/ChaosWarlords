using ChaosWarlords.Source.Entities;
using System.Collections.Generic;
using System;

namespace ChaosWarlords.Source.Utilities
{
    public static class CardFactory
    {
        public static Card CreateSoldier()
        {
            var card = new Card("soldier", "Soldier", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));
            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble()
        {
            var card = new Card("noble", "Noble", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            card.Description = "+1 Influence";
            return card;
        }

        public static Card CreateFromData(CardData data)
        {
            // 1. Parse Aspect
            Enum.TryParse(data.Aspect, true, out CardAspect aspect);

            // 2. Create Card (Mapping DeckVP to main VP for now)
            var card = new Card(data.Id, data.Name, data.Cost, aspect, data.DeckVP, 0);
            card.Description = data.Description;

            // 3. Parse Structured Effects (No more string guessing!)
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

                        // Use the correct constructor (Type, Amount, Resource)
                        card.AddEffect(new CardEffect(type, effectData.Amount, resType));
                    }
                }
            }

            return card;
        }
    }
}