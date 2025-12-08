using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    /// <summary>
    /// Represents a discrete action a card performs.
    /// </summary>
    public class CardEffect
    {
        public EffectType Type { get; set; }
        public int Amount { get; set; }
        public ResourceType TargetResource { get; set; } // e.g., if GainResource, is it Power or Influence?

        public CardEffect(EffectType type, int amount, ResourceType resource = ResourceType.VictoryPoints)
        {
            Type = type;
            Amount = amount;
            TargetResource = resource;
        }
    }
}