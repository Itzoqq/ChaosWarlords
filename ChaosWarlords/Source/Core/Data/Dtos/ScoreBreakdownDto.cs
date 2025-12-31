namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Detailed breakdown of a player's final score.
    /// </summary>
    public class ScoreBreakdownDto
    {
        public int TotalScore { get; set; }
        
        // Components
        public int VPTokens { get; set; }
        public int SiteControlVP { get; set; }
        public int TrophyHallVP { get; set; }
        public int DeckVP { get; set; }
        public int InnerCircleVP { get; set; }
    }
}
