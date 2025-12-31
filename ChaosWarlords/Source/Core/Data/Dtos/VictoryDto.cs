using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Represents the final results of a match.
    /// Used for checking replay outcomes and reporting multiplayer results.
    /// </summary>
    public class VictoryDto
    {
        public bool IsGameOver { get; set; }
        public int? WinnerSeat { get; set; }
        public string? WinnerName { get; set; }
        
        /// <summary>
        /// Map of Player Seat Index -> Final Score
        /// </summary>
        public Dictionary<int, int> FinalScores { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Detailed score breakdown by player seat.
        /// </summary>
        public Dictionary<int, ScoreBreakdownDto> ScoreBreakdowns { get; set; } = new Dictionary<int, ScoreBreakdownDto>();

        /// <summary>
        /// Map of Player Seat Index -> Player Color (int representation or enum if serializable)
        /// We will use string representation for simplicity in DTO.
        /// </summary>
        public Dictionary<int, string> PlayerColors { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// The reason the game ended (e.g. "Market Empty", "Troops Depleted")
        /// </summary>
        public string VictoryReason { get; set; } = string.Empty;
    }
}
