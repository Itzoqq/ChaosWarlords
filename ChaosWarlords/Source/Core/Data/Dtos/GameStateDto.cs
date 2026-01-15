using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Represents a complete snapshot of the game state at a specific point in time.
    /// Root object for Serialization (Save Game / Checkpoints).
    /// </summary>
    public class GameStateDto
    {
        // Meta
        public int Seed { get; set; }
        public int TurnNumber { get; set; }
        public MatchPhase Phase { get; set; }

        // Entities
        public List<PlayerDto> Players { get; set; } = [];
        public MapDto Map { get; set; } = new MapDto();

        // Market (Row of cards)
        public List<CardDto> Market { get; set; } = [];

        // Void (Removed cards)
        public List<CardDto> VoidPile { get; set; } = [];

        // Transient State (Cards pending destruction at end of turn)
        public List<string> MarkedForTurnEndDevourCardIds { get; set; } = [];

        public long SequenceNumber { get; set; }

        public GameStateDto() { }

        public string CalculateChecksum()
        {
            // Simple checksum for strict state verification
            // In a real production scenario, this would be a hash of the entire object graph
            // encapsulated in a deterministic binary serialization.
            // For now, we mix the robust indicators of state.
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Seed.GetHashCode();
                hash = hash * 23 + TurnNumber.GetHashCode();
                hash = hash * 23 + Phase.GetHashCode();
                hash = hash * 23 + SequenceNumber.GetHashCode();
                // Add more fields if needed for deeper verification
                return hash.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
