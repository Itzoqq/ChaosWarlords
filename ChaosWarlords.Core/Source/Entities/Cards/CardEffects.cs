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
        // 1 (Amount == the raw count, no division) for a plain "for each" (e.g. Green Dragon's
        // "1 VP for each site you control"). Effect-type-agnostic: normally resolves a
        // GainResource/DrawCard AMOUNT, but on a repeat-capable effect type (IEffectStrategy.
        // SupportsRepeat) it instead resolves the REPEAT COUNT (Quaggoth: "Assassinate one white
        // troop for each site you control") - see PushEffectContext's own dynamic-repeat-count
        // gate, which also skips the whole effect entirely when that count resolves to 0.
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

        // "Assassinate up to three white troops AT A SINGLE SITE" (Minotaur Skeleton) - a
        // SIBLING constraint to AllowPartialRepeat/SupportsRepeat, not a replacement: the
        // first repeat of this effect can target anywhere a plain Assassinate normally could,
        // but ActionSystem.PerformAssassinate then binds ActionSystem.PendingSite to that
        // target's site (reusing the same field/guard Cloaker's ReturnOwnSpy->Assassinate
        // chain already established - see ActionInputController.HandleAssassinate and
        // AssassinateCommand.Validate), so every later repeat of THIS card's effect is
        // rejected unless it targets a node at that same site. AssassinateStrategy.
        // HasValidTargets also honors this when deciding whether a repeat can still legally
        // continue (Site-restricted board state, not just "any enemy troop anywhere"), so
        // "no more valid targets at this site" resolves the effect early exactly like the
        // plain no-repeat-possible fallback does. Defaults to false so every existing
        // Assassinate/Supplant effect is unaffected. Only meaningful alongside SupportsRepeat
        // (Assassinate) - no shipped card needs this on Supplant/MoveUnit yet.
        public bool RestrictRepeatsToFirstTargetSite { get; set; }

        // "Choose three times: Deploy a troop. Or, Assassinate a white troop." (Weaponmaster) -
        // marks THIS node (an IsOptional effect with a sibling Alternative, e.g. Kobold's exact
        // choice pair) as a repeated INDEPENDENT choice between the 2 branches, resolved
        // ChooseCount times in a row - a genuinely different primitive from SupportsRepeat
        // (repeats the SAME effect type/targeting state N times) and from a plain one-shot
        // IsOptional+Alternative pair (ChooseCount defaults to 0, meaning "not a repeated
        // choice" - every existing card is unaffected). See
        // CardEffectProcessor.ExpandChoiceRepeat for how this is realized: a purely transient
        // OnSuccess/Alternative chain built fresh each time this node is pushed, never written
        // back onto Card.Effects, so the existing resolution engine (ActionExecutionEngine/
        // TryResolveActor/etc.) needs no changes at all to support it. Only the node THIS flag
        // is set on, and its Alternative, are expanded - any OnSuccess/Alternative authored on
        // either of those 2 nodes is only honored on the FINAL round (every earlier round's is
        // overridden by the next round's continuation) - not currently exercised by any shipped
        // card (both are null for Weaponmaster), but left intentionally general. See
        // ExpandChoiceRepeat's own doc comment for a known gap around HasValidTargets lookups
        // and rollback resolving against the wrong (authored, un-expanded) round.
        public int ChooseCount { get; set; }

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

        // "Return any number of your spies -> Supplant a troop at each of the returned
        // spies' sites" (Graz'zt) - marks THIS node (an IsOptional effect with an OnSuccess
        // child, e.g. ReturnOwnSpy.OnSuccess = Supplant) as a repeated PAIR, resolved
        // ChainedRepeatCount times in a row, where EACH round's OnSuccess fires
        // independently right after that round's own step succeeds - a genuinely different
        // primitive from CardEffect.ChooseCount (which repeats a CHOICE between 2 sibling
        // effects, converging into ONE shared OnSuccess only reachable on the final round)
        // and from IEffectStrategy.SupportsRepeat (which repeats the SAME single targeting
        // step N times, converging into its OnSuccess only ONCE at the very end - wrong here,
        // since Graz'zt needs a fresh Supplant, scoped to THAT round's own returned spy's
        // site via the existing ActionSystem.PendingSite chain-link mechanism (see
        // ReturnOwnSpyCommand's doc comment), after every single return - not one shared
        // Supplant after all of them). Defaults to 0, meaning "not a repeated pair" - every
        // existing card is unaffected. See CardEffectProcessor.ExpandChainedRepeat for how
        // this is realized: a purely transient OnSuccess chain built fresh each time this
        // node is pushed (mirroring ExpandChoiceRepeat's exact technique), never written back
        // onto Card.Effects, so the existing resolution engine needs no changes to support it.
        // Only this node's OWN OnSuccess is treated as the per-round repeated step; any
        // Alternative authored on THIS node is preserved as-is (declining it ends the whole
        // sequence early, matching "return any number, including zero"). The per-round step's
        // own Alternative (e.g. Supplant finding no valid target at that specific site) is
        // overridden to also continue to the next round, EXCEPT on the final round, where
        // whatever was authored is preserved - same convergence trick and same known
        // "authored, un-expanded node" StateRestorer/HasValidTargets lookup gap ChooseCount
        // already has (harmless today: neither Graz'zt round varies its own targeting
        // constraints).
        //
        // Interpretive note on Graz'zt's own wording specifically ("return any number of your
        // spies, THEN Supplant a troop at each... site"): read literally this could let a
        // player return several spies first and choose the SUPPLANT order independently of the
        // RETURN order. This strict return-then-immediate-supplant pairing pins supplant order
        // to return order instead. Treated as an equivalent, deliberate reading - the player can
        // already reach any such ordering by choosing which site to return from in which round,
        // and nothing in this game has hidden state an interleaved reveal could expose - not an
        // accidental side effect of reusing the PendingSite chain-link mechanism.
        public int ChainedRepeatCount { get; set; }

        // Set either by CardEffectProcessor.ExpandChainedRepeat (left set on EVERY round
        // including the final one - Graz'zt's ChainedRepeatCount rounds) or authored directly in
        // cards.json (Green Dragon's PlaceSpy -> OnSuccess:Supplant branch). ActionExecutionEngine.
        // HasUnreachableOnSuccess's "don't even ask if accepting would chain into an OnSuccess
        // with no valid target" lookahead is correct for a card like Wight (Devour's only purpose
        // IS enabling the chained Supplant - no point devouring for nothing) but wrong whenever
        // ACCEPTING the top effect is what CREATES the very presence/precondition the chained
        // step needs - the lookahead only ever sees CURRENT board state, before that acceptance
        // happens. Two known cases: Graz'zt ("return a spy" has value on its own - repositioning
        // it to your barracks - independent of whether THIS round's Supplant happens to find a
        // troop at that exact site; without this, a round where global Supplant validity is
        // currently false would be silently skipped entirely, not even offered, which unlike a
        // per-round decline has no Alternative of its own on the round's OWN top node, silently
        // ending the WHOLE "any number" sequence instead of just skipping this one round's
        // Supplant); Green Dragon ("place a spy, THEN supplant a troop at that spy's site" - the
        // spy being placed is what grants Presence there, so a global "is Supplant valid
        // anywhere right now" check can be false purely because that presence doesn't exist YET,
        // even though placing the spy would immediately make it valid). Defaults to false so
        // every existing optional+OnSuccess card keeps the lookahead exactly as before.
        public bool SkipUnreachableOnSuccessCheck { get; set; }

        // "Assassinate up to 3 troops at a single site. For each troop removed, gain
        // Influence" (Death Tyrant) - grants 1 of this resource EACH TIME a repeat of THIS
        // effect actually succeeds, as it happens (ActionSystem.PerformAssassinate), unlike
        // DynamicAmountSource (a single amount computed from live board state, generally AFTER
        // all repeats of a whole effect tree have finished). Defaults to None, meaning "no
        // per-repeat grant" - every existing repeat-capable effect is unaffected. Only wired
        // for Assassinate today - no shipped card needs this on Supplant/MoveUnit yet.
        public ResourceType GainResourcePerRepeat { get; set; }

        // "At end of turn, promote up to 2 other cards played this turn, THEN gain 1 VP for
        // every 3 cards in your inner circle" (Blue Dragon) - a nested effect applied once the
        // deferred end-of-turn promotion-credit redemption THIS EffectType.Promote node granted
        // has actually concluded (every credit either redeemed via a real PromoteCommand or
        // explicitly declined - see TurnContext.RegisterPromotionCompletionEffect/
        // DrainPromotionCompletionEffects and MatchManager.EndTurn). Deliberately NOT modeled as
        // this node's own OnSuccess: PushEffectContext/PushEffectNode would chain that in
        // immediately once THIS Promote node's own EffectContext auto-resolves, which - because
        // EffectType.Promote is a non-targeting/automatic effect - happens the moment the card
        // is PLAYED, long before the deferred redemption itself (possibly many other actions
        // later, or declined outright). Counting "cards in your inner circle" at that earlier
        // moment would silently exclude whatever THIS card's own redemption is about to promote.
        // Only meaningful on EffectType.Promote; null (no completion effect) for every other
        // Promote card today (core_noble, Cultist of Myrkul, Zuggtmoy).
        public CardEffect? PromotionCompletionEffect { get; set; }

        // "Choose an opponent with a troop adjacent to at least 1 of them [the just-deployed
        // troops]" (Gibbering Mouther) - switches EffectType.SelectOpponent's eligibility check
        // from its default "hand size exceeds Amount" threshold (Cranium Rats) to "has a troop
        // on a node adjacent to any of ActionSystem.PendingDeployedNodes" (see
        // SelectOpponentEligibility). Defaults to false so every existing SelectOpponent effect
        // keeps the hand-size threshold unchanged. Only meaningful directly following an
        // EffectType.DeployTroop effect - no shipped card needs it otherwise.
        public bool RequiresAdjacencyToRecentDeploys { get; set; }

        // Which card definition EffectType.ForceRecruit gives to whoever it resolves against
        // (e.g. "insane_outcast") - looked up via ICardDatabase.GetCardById, bypassing the
        // market row entirely. Null is a configuration error (logged, no-op) for any card
        // actually using ForceRecruit; every other effect type ignores this field.
        public string? TargetCardId { get; set; }

        // "Each opponent recruits N Insane Outcasts" (Demogorgon/Ghoul) - switches
        // EffectType.ForceRecruit from its default single-recipient behavior (context.
        // ActivePlayer, e.g. whoever EffectType.SelectOpponent chose, as with Gibbering
        // Mouther) to looping over EVERY opponent of the real card-playing player
        // (TurnManager.GetOpponentsInSeatOrder), giving each one a copy. Defaults to false so
        // Gibbering Mouther's existing single-recipient ForceRecruit is unaffected. Only
        // meaningful on a top-level ForceRecruit effect - never chained under SelectOpponent,
        // which already names a single recipient of its own.
        public bool AppliesToEachOpponent { get; set; }

        // "At end of turn, promote an Obedience card played this turn" (Air/Fire/Water
        // Elemental Myrmidon) - restricts an EffectType.Promote credit to only be redeemable
        // against a card of THIS specific aspect, threaded onto the TurnContext.PromotionCredit
        // this effect banks (see TurnContext.AddPromotionCredit/HasValidCreditFor/
        // ConsumeCreditFor). Null (the default) means no filter at all - every existing Promote
        // effect (core_noble, Cultist of Myrkul, Zuggtmoy, Blue Dragon) is unaffected. A SIBLING
        // restriction to the credit's own self-exclusion (a card can never promote itself,
        // filter or no filter) - not a replacement for it.
        public CardAspect? RequiredPromotionAspect { get; set; }

        // "...promote ANY NUMBER of Undead cards played this turn" (High Priest of Myrkul) - a
        // sibling restriction to RequiredPromotionAspect, filtering on CardCreatureType instead
        // of CardAspect. Independent of PromoteAnyNumber below (a plain fixed-Amount Promote
        // credit could theoretically carry a creature-type filter too, though no shipped card
        // does yet) - the two are orthogonal knobs on the same EffectType.Promote shape.
        public CardCreatureType? RequiredPromotionCreatureType { get; set; }

        // "...promote ANY NUMBER of..." (as opposed to a fixed Amount, e.g. core_noble's 1 or
        // Cultist of Myrkul's "up to 2") - when true, ApplyPromote banks an UNBOUNDED credit
        // (TurnContext.AddUnboundedPromotionCredit) instead of Amount discrete ones: exactly one
        // redeemable credit per currently-eligible played card, computed once redemption
        // actually starts (by which point no more cards can be played this turn), rather than a
        // literal integer authored in cards.json. Always implicitly optional - "any number,
        // including zero" can never be mandatory - so PromotionCreditIsOptional is irrelevant
        // when this is set. Effect.Amount is ignored entirely when this is true.
        public bool PromoteAnyNumber { get; set; }

        // "Return another player's troop or spy" (High Priest of Myrkul) - restricts
        // EffectType.ReturnUnitOrSpy's target-type union (own-or-enemy troop-or-spy, Intellect
        // Devourer's shape) to enemy-only, filling the gap between that effect (too broad) and
        // EffectType.ReturnEnemySpy (enemy-only but spy-only, too narrow). Defaults to false so
        // Intellect Devourer's existing own-or-enemy behavior is unaffected.
        public bool ReturnEnemyOnly { get; set; }

        public CardEffect(EffectType type, int amount, ResourceType targetResource = ResourceType.None)
        {
            Type = type;
            Amount = amount;
            TargetResource = targetResource;
        }

        public bool ReplaceWithSource { get; internal set; }
    }
}

