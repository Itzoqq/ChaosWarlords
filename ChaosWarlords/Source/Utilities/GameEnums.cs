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
        SelectingSpyToReturn
    }

    // Replaces the "Suits" (Conquest, Malice, Guile, Obedience)
    public enum CardAspect
    {
        Neutral = 0,    // Starter cards (Minions/Nobles)
        Warlord,        // Aggressive (Conquest) - Best at taking over the Underdark 
        Sorcery,        // Magic/Control (Malice) - Best at assassination 
        Shadow,         // Spies/Assassination (Guile) - Best at spying 
        Order,          // Defense/Movement (Obedience) - Day-to-day tasks 
        Ambition        // Recruitment/Inner Circle (Ambition) - Best at recruiting & promoting 
    }

    public enum ResourceType
    {
        None = 0,
        Influence,  // Used to buy cards (Spider/Web resource)
        Power,      // Used to deploy units/assassinate (Military resource)
        VictoryPoints
    }

    public enum CardLocation
    {
        None = 0,
        Market,
        Hand,
        Deck,
        DiscardPile,
        InnerCircle,   // The "Promoted" pile (Tyrants' Inner Circle)
        Void            // Removed from game entirely
    }

    // The command pattern: what does this card actually DO?
    public enum EffectType
    {
        GainResource,
        DeployUnit,
        Assassinate,
        ReturnUnit,
        Supplant,
        Promote,
        DrawCard,
        Devour,
        PlaceSpy
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
        Info
    }
}