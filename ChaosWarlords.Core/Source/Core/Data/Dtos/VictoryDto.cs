namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Represents the final results of a match.
    /// Used for checking replay outcomes and reporting multiplayer results.
    /// </summary>
    public class VictoryDto
    {
        public bool IsGameOver { get; set; }

        /// <summary>
        /// Seat index of every player sharing the win. Per the rulebook (p.14), a tie
        /// for the highest score is a shared win, not resolved by any tiebreaker - this
        /// holds more than one seat whenever 2+ players are tied for the highest score.
        /// </summary>
        public List<int> WinnerSeats { get; set; } = new List<int>();

        /// <summary>
        /// Display name of every player sharing the win, in the same order as <see cref="WinnerSeats"/>.
        /// </summary>
        public List<string> WinnerNames { get; set; } = new List<string>();

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
