namespace ChaosWarlords.Source.Utilities
{

    // 1. Define the States
    public enum ActionState
    {
        Normal,
        TargetingAssassinate,
        TargetingReturn,
        TargetingSupplant,
        TargetingPlaceSpy,
        TargetingReturnSpy,
        SelectingSpyToReturn,
        SelectingCardToPromote,
        TargetingMoveSource,
        TargetingMoveDestination,
        TargetingDevourHand,
        TargetingDevourMarket,
        TargetingDevourInnerCircle,
        TargetingDiscard, // Forced discard from a specific player's own hand (Insane Outcast's self-discard, Neogi's cross-player forced discard)
        TargetingReturnOwnSpy, // Return one of the active player's OWN spies (e.g. Cloaker), as opposed to TargetingReturnSpy (enemy spy)
        TargetingPlayFromMarket, // Picking a market card to play "as if in hand" (e.g. Ulitharid) - see ActionSystem.TryStartPlayFromMarket
        TargetingOpponentSelect, // Choosing which opponent to target with EffectType.SelectOpponent (e.g. Cranium Rats' "choose one opponent... to discard")
        TargetingPromoteFromPile, // Picking a card to promote RIGHT NOW from an expanded pool
                                 // (discard pile, or hand+discard+self) via EffectType.
                                 // PromoteFromPile (e.g. Matron Mother, Necromancer) - distinct
                                 // from SelectingCardToPromote, which is the legacy deferred
                                 // end-of-turn promotion-credit flow (Noble/Cultist of Myrkul)
                                 // wired to PromoteInputMode; do not conflate the two.
        TargetingReturnUnitOrSpy, // A single targeting step that accepts EITHER a node click
                                 // (return the troop there, via the existing ReturnTroopCommand/
                                 // CanReturnTroop - already symmetric own/enemy) OR a site click
                                 // (return a spy there, via the new ReturnAnySpyCommand) -
                                 // EffectType.ReturnUnitOrSpy's "target-type union" (Intellect
                                 // Devourer: "Return up to two troops or spies"). Deliberately a
                                 // single flat state, not 2 sub-states like MoveUnit's source/
                                 // destination pair - both target types resolve in one click.
        TargetingDeployFromTrophyHall, // A single node-click destination step (mirrors
                                 // TargetingMoveDestination: "any empty troop space, no Presence
                                 // needed") for EffectType.DeployFromTrophyHall (Mummy Lord:
                                 // "take a white troop from any trophy hall and deploy it
                                 // anywhere on the board"). WHICH player's trophy hall to draw
                                 // from is resolved automatically before this state is entered
                                 // (ActionSystem.PendingTrophyHallSourceColor) - not a separate
                                 // click, see that property's own doc comment for why.
        TargetingDeployTroop // EffectType.DeployTroop (Gibbering Mouther: "Deploy 2 troops,
                                 // then choose an opponent with a troop adjacent to at least 1
                                 // of them") - an IMMEDIATE, stack-integrated node-click Deploy,
                                 // funded the same way GainResource(Troops) is (Player.
                                 // PendingFreeTroops, credited then consumed right away instead
                                 // of deferred) but distinct from it: GainResource(Troops) only
                                 // ever credits a pool the player spends LATER via the plain
                                 // basic-action DeployTroopCommand (untracked, disconnected from
                                 // any card's own resolution), which cannot support "then..."
                                 // follow-up chains that need to know WHERE the troops landed.
                                 // Each repeat's destination node is recorded onto ActionSystem.
                                 // PendingDeployedNodes for exactly that purpose.
    }

    // Replaces the "Suits" (Conquest, Malice, Guile, Obedience)
    public enum CardAspect
    {
        Neutral = 0,    // Starter cards (Minions/Nobles)
        Warlord,        // Aggressive (Conquest) - Best at taking over the Underdark 
        Sorcery,        // Magic/Control (Malice) - Best at assassination 
        Shadow,         // Spies/Assassination (Guile) - Best at spying 
        Order,          // Defense/Movement (Obedience) - Day-to-day tasks 
        Blasphemy,       // Recruitment/Inner Circle (Ambition) - Best at recruiting & promoting
        Oblivion        // Void/Devour themed
    }

    public enum ResourceType
    {
        None = 0,
        Influence,  // Used to buy cards (Spider/Web resource)
        Power,      // Used to deploy units/assassinate (Military resource)
        VictoryPoints,
        Troops      // Direct troop gain to barracks
    }

    public enum CardLocation
    {
        None = 0,
        Market,
        Hand,
        Played,
        Deck,
        DiscardPile,
        InnerCircle,   // The "Promoted" pile (Tyrants' Inner Circle)
        Void,          // Removed from game entirely
        Self,          // The card itself (for self-devour effects)
        Supply,        // Returned to the shared supply (e.g. Insane Outcast) - distinct from
                        // Void: not actually devoured, can re-enter play via whatever grants it
        HandOrDiscard  // EffectType.PromoteFromPile ONLY: expands the pool to Hand + DiscardPile
                        // + the source card itself (e.g. Necromancer - "promote this card, or a
                        // card from your hand or discard pile"). CardLocation.DiscardPile alone
                        // means "Discard pile only" for the same effect (e.g. Matron Mother).
    }

    // The command pattern: what does this card actually DO?
    public enum EffectType
    {
        None = 0,
        GainResource,
        Assassinate,
        ReturnUnit,
        Supplant,
        Promote,
        DrawCard,
        Devour,
        PlaceSpy,
        MoveUnit,
        DiscardCard,
        MarkOpponentDiscardAtEndOfTurn, // Non-targeting: just banks a MatchContext.PendingOpponentDiscardTriggers entry, resolved by MatchManager.EndTurn's opponent-discard phase
        ReturnOwnSpy, // Return one of the active player's OWN spies (e.g. Cloaker) - see TargetingReturnOwnSpy
        PlayFromMarket, // Play a market card "as if in hand", then it gets devoured (e.g. Ulitharid) - Amount is the max cost

        // Generic "target a player" primitive - the active player chooses one opponent
        // matching an eligibility threshold (Amount = minimum hand size the opponent must
        // exceed to be eligible), then that opponent becomes TurnManager.ForcedActingPlayer
        // for whatever OnSuccess chains off it (e.g. Cranium Rats chains DiscardCard). First
        // reusable instance of this shape in the codebase - see planning.txt TIER 2 #6.
        SelectOpponent,

        // Puts the active player's entire draw pile into their discard pile (Matron Mother's
        // first half). Automatic/instant - no targeting, no strategy registration (falls
        // through to DefaultStrategy). See Player.MoveDeckToDiscard/Deck.MoveAllToDiscard.
        MoveDeckToDiscard,

        // Immediately promote a specific card, chosen RIGHT NOW during this card's own
        // resolution (blocking on the execution stack), from an expanded pool - see
        // CardLocation.HandOrDiscard/DiscardPile for what TargetLocation selects. Distinct
        // from EffectType.Promote (the deferred end-of-turn promotion-credit flow) - see
        // planning.txt TIER 2 #2.
        PromoteFromPile,

        // Unconditionally promotes THIS card itself (the one carrying the effect), deferred to
        // end of turn - e.g. Revenant: "...if you have 8 or more troops in your trophy hall,
        // promote this card." Distinct from EffectType.Promote, which banks a credit
        // redeemable against OTHER eligible cards (TurnContext.ConsumeCreditFor explicitly
        // excludes the credit's own source card) - there is no player choice here at all, and
        // no PromotionCreditIsOptional "up to N" shape. Non-targeting/automatic (falls through
        // to DefaultStrategy) - CardEffect.Condition is what makes this conditional. See
        // MatchContext.CardsMarkedForTurnEndPromote.
        PromoteSelf,

        // "Return up to two troops or spies" (Intellect Devourer) - a target-type UNION: each
        // repeat can independently be a node click (return the troop there - reuses
        // ReturnTroopCommand/MapManager.CanReturnTroop as-is, already own/enemy-symmetric) or a
        // site click (return a spy there - the new ReturnAnySpyCommand/MapManager.
        // CanReturnAnySpy, same own/enemy symmetry). Distinct from EffectType.ReturnUnit (troops
        // only) and EffectType.ReturnOwnSpy (the active player's own spy only, no target-type
        // choice at all). See ActionState.TargetingReturnUnitOrSpy and
        // ReturnUnitOrSpyStrategy.
        ReturnUnitOrSpy,

        // "Take a white troop from any trophy hall and deploy it anywhere on the board" (Mummy
        // Lord) - the first TROPHY-HALL-AS-TROOP-RESERVOIR card (planning.txt): sources a Deploy
        // from a trophy hall's captured-troop composition (Player.TrophyHallByColor) instead of
        // the normal barracks/PendingFreeTroops supply, then places it as the ACTIVE player's
        // OWN troop (matching the rulebook's "Deploy" always means placing one of YOUR troops -
        // this effect only changes WHERE that troop is funded from, same as Supplant's deploy
        // half is always free regardless of barracks state). CardEffect.TargetNeutralTroopOnly
        // filters which trophy-hall composition color is eligible (true for Mummy Lord's "white"
        // troop; false would mean "any color," not yet needed by a shipped card - see
        // TrophyHallRuleEngine's own doc comment for why that shape isn't built yet). See
        // ActionState.TargetingDeployFromTrophyHall and DeployFromTrophyHallStrategy.
        DeployFromTrophyHall,

        // "Return an enemy spy" (Red Dragon) - reuses the SAME ActionState.TargetingReturnSpy/
        // SpySubsystem.HandleReturnSpyInitialClick/ResolveSpyCommand machinery the paid basic
        // action already uses (ResolveSpyCommand already waives its Power cost whenever CardId
        // is set, so it was already card-effect-ready), including that flow's own multi-enemy-
        // spy disambiguation (ActionSystem.TransitionToSpySelection) - unlike
        // EffectType.ReturnUnitOrSpy's simpler site-click path, a site with 2+ enemy spies is
        // still a fully resolvable target here. Distinct from ReturnOwnSpy (the active player's
        // OWN spy only) and ReturnUnitOrSpy (troop-or-spy, own-or-enemy - too broad for "an
        // enemy spy" specifically). See ReturnEnemySpyStrategy and MapRuleEngine.
        // HasValidReturnEnemySpyTarget.
        ReturnEnemySpy,

        // "Deploy 2 troops, then choose an opponent with a troop adjacent to at least 1 of
        // them" (Gibbering Mouther) - see ActionState.TargetingDeployTroop's doc comment for why
        // this is distinct from GainResource(Troops). See DeployTroopStrategy and
        // ActionSystem.PerformDeployTroop/PendingDeployedNodes.
        DeployTroop,

        // Immediately gives a SPECIFIC card (CardEffect.TargetCardId, e.g. "insane_outcast",
        // CardEffect.Amount copies of it, minimum 1) to one or more players - non-targeting/
        // automatic, falls through to DefaultStrategy. Two shapes, both shipped:
        // (1) chained as an OnSuccess off EffectType.SelectOpponent (Gibbering Mouther), where
        // "the active player" at the moment this resolves is whichever single opponent
        // SelectOpponent chose (TurnManager.ForcedActingPlayer), never the real card-playing
        // player; (2) authored directly as a top-level effect with CardEffect.
        // AppliesToEachOpponent set (Demogorgon/Ghoul's "each opponent recruits N Insane
        // Outcasts") - no SelectOpponent step at all, every opponent of the real card-playing
        // player is given a copy, in TurnManager.GetOpponentsInSeatOrder order. Bypasses the
        // market row entirely via ICardDatabase.GetCardById, matching CardDatabase.
        // GetAllMarketCards' own doc comment that supply-pile cards (RedirectsToSupplyOnDevour
        // OrPromote) "only ever reach a player via another card's effect." Lands in the
        // player's discard pile via IPlayerStateManager.AcquireCard, the same destination a
        // normal paid recruit uses - "recruit" always means the same thing regardless of who
        // paid for it.
        ForceRecruit,
    }

    /// <summary>
    /// Types of conditions that gate card effect execution.
    /// Used by EffectCondition to evaluate "If you control a Site" type logic.
    /// </summary>
    public enum ConditionType
    {
        None,                   // No condition - always executes
        ControlsSite,          // Player controls at least one Site
        HasTroopsDeployed,     // Player has troops on the map
        HasResourceAmount,     // Player has X or more of a resource
        InnerCircleCount,      // Player has X or more cards in Inner Circle
        HandSize,               // Player has X or more cards in hand
        OpponentPresentAtSite, // Another player has a spy/troop (see SitePresenceType) at
                                // ActionSystem.PendingSite (e.g. Banshee, Infiltrator)
        TrophyHallCount        // Player has X or more troops in their trophy hall (e.g.
                                // Revenant: "if you have 8 or more troops in your trophy hall")
    }

    /// <summary>
    /// What kind of presence ConditionType.OpponentPresentAtSite checks for at
    /// ActionSystem.PendingSite - distinguishes "another player has a SPY here" (Banshee)
    /// from "another player has a TROOP here" (Infiltrator).
    /// </summary>
    public enum SitePresenceType
    {
        Spy,
        Troop
    }

    /// <summary>
    /// Where CardEffectProcessor.ResolveAmount computes an effect's actual amount from live
    /// game state at resolution time, instead of using CardEffect.Amount as a fixed literal
    /// (e.g. White Dragon: "Gain 1 VP for every 2 sites you control" - the real amount depends
    /// on board state when the card resolves, not a number baked into cards.json). Defaults to
    /// None, meaning "use CardEffect.Amount as-is" - every existing card is unaffected.
    /// </summary>
    public enum DynamicAmountSource
    {
        None = 0,
        SitesControlled, // Count of Sites where Site.Owner == the active player's color
                         // (White Dragon: "for every 2"; Green Dragon: a plain "for each," same
                         // source, DynamicAmountDivisor simply omitted). Distinct from
                         // SitesUnderTotalControl below (a stricter subset).
        TrophyHallCount, // Player.TrophyHall (total troops of any color in the active
                         // player's trophy hall) at resolution time (Beholder: "Gain
                         // Influence for every 3 troops in your trophy hall").
        PlayerTrophyHallCount, // Sum of Player.TrophyHallByColor EXCLUDING PlayerColor.Neutral/
                         // None - i.e. only actual opposing players' troops, not captured white/
                         // unaligned ones (Death Knight: "Gain 1 VP for every 5 player troops in
                         // your trophy hall" - the printed card's own wording, distinct from
                         // TrophyHallCount's "any troop" total).
        InnerCircleCount, // Player.InnerCircle.Count at resolution time (Vampire: "Promote a
                         // card from your discard pile, then gain 1 VP for every 3 cards in your
                         // inner circle" - counted AFTER that same card's own Promote has added
                         // to it, since GainResource is chained via OnSuccess off PromoteFromPile).
        SpiesOnBoard,    // Count of Sites where Site.HasSpy(activePlayerColor) is true (Aboleth:
                         // "Draw a card for each spy you have on the board") - the first
                         // DynamicAmountSource consumed by EffectType.DrawCard rather than
                         // GainResource; ResolveAmount itself is effect-type-agnostic already.
        NeutralTrophyHallCount, // Player.TrophyHallByColor[PlayerColor.Neutral] ONLY - i.e. just
                         // captured white/unaligned troops, the mirror image of
                         // PlayerTrophyHallCount's "everyone EXCEPT Neutral" (Black Dragon:
                         // "Gain 1 VP for every 3 WHITE troops in your trophy hall" - the
                         // printed card's own wording, distinct from TrophyHallCount's "any
                         // troop" total).
        SitesUnderTotalControl // Count of Sites where Site.Owner == the active player's color
                         // AND Site.HasTotalControl is true (Red Dragon: "Gain 1 VP for each
                         // site under your total control") - a stricter subset of
                         // SitesControlled above (mere control, majority troops only).
    }

    public enum PlayerColor
    {
        None = 0,       // Empty space
        Neutral,    // White troops (Unaligned enemies)
        Red,        // Player 1
        Blue,       // Player 2
        Black,      // Player 3
        Orange      // Player 4
    }

    public enum LogChannel
    {
        General,
        Input,
        Combat,
        Economy,
        AI,
        Error,
        Warning,
        Info,
        Debug
    }

    /// <summary>
    /// Represents the current mode of market interaction.
    /// </summary>
    public enum MarketMode
    {
        /// <summary>Market is not visible</summary>
        Closed,
        
        /// <summary>Normal browsing/buying mode</summary>
        Browse,
        
        /// <summary>Devour targeting mode (selecting card to remove from game)</summary>
        DevourTarget
    }
}

