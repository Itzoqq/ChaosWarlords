using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    public class CardEffect
    {
        // Public Read: UI needs to show "Gain 3 Power"
        // Internal Set: Only CardFactory creates these
        public EffectType Type { get; set; }
        public int Amount { get; set; }
        public ResourceType TargetResource { get; set; }
        public bool RequiresFocus { get; set; }
        public CardEffect? OnSuccess { get; set; }

        // "Choose one" support: sibling to OnSuccess, not nested under it. OnSuccess means
        // "and then, if this succeeded"; Alternative means "instead, if this was declined or
        // impossible". See CardEffectProcessor.ResolveEffects/PushEffectNode.
        public CardEffect? Alternative { get; set; }

        // Conditional Logic Support
        public EffectCondition? Condition { get; set; }          // "If you control a Site"
        public bool IsOptional { get; set; }                     // "You may..."
        public CardLocation TargetLocation { get; set; } = CardLocation.None; // Where the target is from (Market, Deck, etc.)

        // "Assassinate/Supplant a white troop" (rulebook: restricted to an unaligned/Neutral
        // troop only, never another player's) - Ravenous Zombies is the first shipped card using
        // this. Defaults to false (no filter) so every existing Assassinate/Supplant effect is
        // unaffected. See planning.txt TIER 2 #1 for more cards wanting the same filter later.
        public bool TargetNeutralTroopOnly { get; set; }

        public CardEffect(EffectType type, int amount, ResourceType targetResource = ResourceType.None)
        {
            Type = type;
            Amount = amount;
            TargetResource = targetResource;
        }

        public bool ReplaceWithSource { get; internal set; }
    }
}

