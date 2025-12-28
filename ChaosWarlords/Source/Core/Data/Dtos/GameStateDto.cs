using System.Collections.Generic;
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

        public GameStateDto() { }
    }
}
