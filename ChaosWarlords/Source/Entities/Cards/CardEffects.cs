using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    public class CardEffect
    {
        // Public Read: UI needs to show "Gain 3 Power"
        // Internal Set: Only CardFactory creates these
        public EffectType Type { get; internal set; }
        public int Amount { get; internal set; }
        public ResourceType TargetResource { get; internal set; }
        public bool RequiresFocus { get; internal set; }
        public CardEffect? OnSuccess { get; internal set; }

        // Conditional Logic Support
        public EffectCondition? Condition { get; internal set; }          // "If you control a Site"
        public bool IsOptional { get; internal set; }                     // "You may..."

        public CardEffect(EffectType type, int amount, ResourceType targetResource = ResourceType.None)
        {
            Type = type;
            Amount = amount;
            TargetResource = targetResource;
        }
    }
}

