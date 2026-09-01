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
        TargetingPlayFromMarket // Picking a market card to play "as if in hand" (e.g. Ulitharid) - see ActionSystem.TryStartPlayFromMarket
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
        Supply         // Returned to the shared supply (e.g. Insane Outcast) - distinct from
                        // Void: not actually devoured, can re-enter play via whatever grants it
    }

    // The command pattern: what does this card actually DO?
    public enum EffectType
    {
        None = 0,
        GainResource,
        DeployUnit,
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
        HandSize               // Player has X or more cards in hand
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

