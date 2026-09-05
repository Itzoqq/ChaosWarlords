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

        // "Supplant a white troop anywhere on the board" (rulebook: an explicit override of
        // the normal Presence requirement, tyrants-rules.pdf p.9/22) - Ogre Zombie is the
        // first shipped card using this. Defaults to false (normal Presence still required)
        // so every existing Assassinate/Supplant effect is unaffected. Only threaded through
        // the Supplant path - no shipped card needs this on plain Assassinate.
        public bool IgnoresPresenceRequirement { get; set; }

        // "Gain 1 VP for every 2 sites you control" (White Dragon) - when set to anything but
        // None, CardEffectProcessor.ResolveAmount computes the actual amount from live game
        // state instead of using Amount as a fixed literal. DynamicAmountDivisor is the "every
        // N" part (integer division, floor - 3 sites at divisor 2 is 1 VP, not 1.5); defaults to
        // 1 (Amount == the raw count, no division) for a source like Green/Red Dragon's "for
        // each" - not yet wired, since neither has a shipped card, but the field already
        // supports that ratio once one is.
        public DynamicAmountSource DynamicAmountSource { get; set; }
        public int DynamicAmountDivisor { get; set; } = 1;

        // Outcome-dependent targeting (Mindwitness: "Assassinate a troop. If that troop
        // belonged to another player... they must discard a card.") - this effect's actor is
        // not the card's owner, but whoever ActionSystem.PendingAffectedPlayerColor names (the
        // player whose troop/spy the immediately preceding Assassinate/Supplant step just
        // removed). Only meaningful on an OnSuccess/Alternative node chained directly beneath
        // an Assassinate/Supplant effect; CardEffectProcessor.PushEffectContext resolves it
        // (falling through to Alternative, same as an unmet Condition/no-valid-targets, if
        // PendingAffectedPlayerColor isn't a real opponent - e.g. the removed troop was
        // Neutral). Defaults to false so every existing effect is unaffected.
        public bool TargetsAffectedPlayer { get; set; }

        // "Move up to 2 enemy troops" (Council Member) - marks a SupportsRepeat effect as
        // voluntarily stoppable early: CardEffect.Amount is a MAXIMUM, not a mandatory exact
        // count. Deathblade's "Assassinate 2 troops" doesn't set this - it can only stop
        // early via ShouldRepeatCurrentEffect's own "no more legal targets" fallback, never by
        // player choice while a legal target still exists. See
        // ActionExecutionEngine.DeclineRemainingRepeats and DeclineRepeatCommand. Defaults to
        // false so every existing repeat-capable effect is unaffected.
        public bool AllowPartialRepeat { get; set; }

        // "At end of turn, promote up to 2 other cards played this turn" (Cultist of Myrkul,
        // Zuggtmoy) - marks an EffectType.Promote effect's banked end-of-turn credits as
        // voluntarily declinable, as opposed to the plain "promote a card played this turn"
        // shape (e.g. core_noble), which the rulebook's plain instruction-following rule
        // (tyrants-rules.pdf p.9) makes mandatory once a valid target exists. Threaded onto
        // each TurnContext.PromotionCredit this effect banks (see TurnContext.
        // AddPromotionCredit) - a DIFFERENT mechanism from CardEffect.AllowPartialRepeat above
        // (that one governs the immediate, execution-stack-blocking repeat primitive; this one
        // governs the separate deferred end-of-turn promotion-credit flow, redeemed via
        // PromoteInputMode). Defaults to false so every existing Promote effect is unaffected.
        public bool PromotionCreditIsOptional { get; set; }

        public CardEffect(EffectType type, int amount, ResourceType targetResource = ResourceType.None)
        {
            Type = type;
            Amount = amount;
            TargetResource = targetResource;
        }

        public bool ReplaceWithSource { get; internal set; }
    }
}

