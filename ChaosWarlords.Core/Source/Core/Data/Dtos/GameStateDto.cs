using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Utilities;

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

        // Stack State (For mid-action recovery)
        public List<EffectContextDto> EffectStack { get; set; } = [];

        public long SequenceNumber { get; set; }

        public GameStateDto() { }

        public string CalculateChecksum()
        {
            // Simple checksum for strict state verification.
            // Uses StateHasher (FNV-1a) rather than GetHashCode(), which is not guaranteed
            // stable across .NET versions/platforms and would cause spurious desyncs between
            // a server and client running different runtimes.
            // In a real production scenario this would hash the entire object graph via a
            // deterministic serialization; for now we mix the top-level state indicators.
            int hash = StateHasher.ComputeHash(Seed, TurnNumber, Phase, SequenceNumber);
            return hash.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
