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

        // Stack State (For mid-action recovery)
        public List<EffectContextDto> EffectStack { get; set; } = [];

        public long SequenceNumber { get; set; }

        /// <summary>
        /// Deterministic hash of the full game state at snapshot time, for desync detection
        /// (e.g. comparing a server's and a client's state after the same sequence of
        /// commands). Populated by DtoMapper.ToGameStateDto from MatchContext.GetStateHash() -
        /// this DTO does NOT compute its own hash. It used to (a `CalculateChecksum()` method
        /// using StateHasher.ComputeHash directly), but that mixed only Seed/TurnNumber/
        /// Phase/SequenceNumber - no map, player, or market state - so it could not actually
        /// have caught a desync outside of those four fields, despite living on exactly the
        /// object that would travel over the wire for that purpose. See planning.txt.
        /// </summary>
        public string StateHash { get; set; } = string.Empty;

        public GameStateDto() { }
    }
}
