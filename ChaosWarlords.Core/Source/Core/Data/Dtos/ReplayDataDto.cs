namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Wrapper for all data required to faithfully reproduce a game session.
    /// Includes the initial RNG seed and the sequence of all player actions.
    /// </summary>
    public class ReplayDataDto
    {
        /// <summary>
        /// The initial seed used for the match's random number generator.
        /// </summary>
        public int Seed { get; set; }
        public string FirstMarketAspect { get; set; } = "Warlord";
        public string SecondMarketAspect { get; set; } = "Sorcery";

        /// <summary>
        /// Ordered collection of all commands executed during the session.
        /// </summary>
        public List<GameCommandDto> Commands { get; set; } = new List<GameCommandDto>();
    }
}
