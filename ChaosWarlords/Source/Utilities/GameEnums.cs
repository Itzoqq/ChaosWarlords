namespace ChaosWarlords.Source.Utilities
{
    // Replaces the "Suits" (Conquest, Malice, Guile, Obedience)
    public enum CardAspect
    {
        Neutral,    // Starter cards (Minions/Nobles)
        Warlord,    // Aggressive (Conquest)
        Sorcery,    // Magic/Control (Malice)
        Shadow,     // Spies/Assassination (Guile)
        Order       // Defense/Movement (Obedience)
    }

    public enum ResourceType
    {
        Influence,  // Used to buy cards (Spider/Web resource)
        Power,      // Used to deploy units/assassinate (Military resource)
        VictoryPoints
    }

    public enum CardLocation
    {
        Market,
        Hand,
        Deck,
        DiscardPile,
        InnerCircle,   // The "Promoted" pile (Tyrants' Inner Circle)
        Void           // Removed from game entirely
    }

    // The command pattern: what does this card actually DO?
    public enum EffectType
    {
        GainResource,       // +2 Power
        DeployUnit,         // Place unit on board
        Assassinate,        // Remove enemy unit
        ReturnUnit,         // Return unit to hand
        Promote,            // Move card to Inner Circle (Trash for VP)
        DrawCard,           // Draw X cards
        Devour              // Trash a card from hand/discard
    }

    public enum PlayerColor
    {
        None,       // Empty space
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
        Error
    }
}