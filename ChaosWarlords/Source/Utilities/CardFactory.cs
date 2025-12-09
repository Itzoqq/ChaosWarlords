using ChaosWarlords.Source.Entities;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.Utilities
{
    public static class CardFactory
    {
        // We will pass the 1x1 pixel texture here for now to act as the card background
        public static Card CreateSoldier(Texture2D texture)
        {
            // Equivalent to Tyrants "Minion"
            var card = new Card("starter_soldier", "Chaos Grunt", 0, CardAspect.Neutral, 0, 1);

            // Effect: +1 Power
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));

            card.SetTexture(texture);
            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble(Texture2D texture)
        {
            // Equivalent to Tyrants "Noble"
            var card = new Card("starter_noble", "Corrupt Noble", 0, CardAspect.Neutral, 1, 2);

            // Effect: +1 Influence
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));

            card.SetTexture(texture);
            card.Description = "+1 Influence";
            return card;
        }
    }
}