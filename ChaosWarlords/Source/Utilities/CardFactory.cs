using ChaosWarlords.Source.Entities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Utilities
{
    public static class CardFactory
    {
        public static Card CreateSoldier()
        {
            // "Soldier" is a starter card -> Aspect.Neutral
            var card = new Card("soldier", "Soldier", 0, CardAspect.Neutral, 0, 0);

            // FIXED: Order is (Type, Amount, Resource)
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));

            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble()
        {
            // "Noble" is a starter card -> Aspect.Neutral
            var card = new Card("noble", "Noble", 0, CardAspect.Neutral, 0, 0);

            // Order is (Type, Amount, Resource)
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));

            card.Description = "+1 Influence";
            return card;
        }

        public static Card CreateFromData(CardData data)
        {
            // This tries to parse string "Warlord", "Sorcery" etc. from JSON into the Enum
            System.Enum.TryParse(data.Aspect, true, out CardAspect aspect);

            var card = new Card(data.Id, data.Name, data.Cost, aspect, data.VictoryPoints, 0);

            // Simple parsing logic for the "Vertical Slice"
            if (data.Text.Contains("+") && data.Text.Contains("Power"))
            {
                // Assuming +1 for simple parsing if number isn't easily extracted
                card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));
            }
            else if (data.Text.Contains("+") && data.Text.Contains("Influence"))
            {
                card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            }

            // In a real implementation, you would write a text parser here to read "Gain 3 Power"
            // and extract the '3' into the amount variable.

            card.Description = data.Text;
            return card;
        }
    }
}