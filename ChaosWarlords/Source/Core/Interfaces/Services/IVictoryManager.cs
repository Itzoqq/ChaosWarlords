using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Manages victory conditions and final scoring calculations.
    /// Determines when the game ends and who wins.
    /// </summary>
    public interface IVictoryManager
    {
        /// <summary>
        /// Checks if any end-game conditions have been met.
        /// End-game triggers: Player out of troops OR market deck empty.
        /// </summary>
        /// <param name="context">The current match context.</param>
        /// <param name="reason">The reason the game ended, if applicable.</param>
        /// <returns>True if the game should end; otherwise, false.</returns>
        bool CheckEndGameConditions(MatchContext context, out string reason);

        /// <summary>
        /// Calculates the final score for a player including all VP sources.
        /// </summary>
        /// <param name="player">The player to score.</param>
        /// <param name="context">The match context for site control calculations.</param>
        /// <returns>The player's total final score.</returns>
        int CalculateFinalScore(Player player, MatchContext context);

        /// <summary>
        /// Gets a detailed breakdown of the player's score.
        /// </summary>
        ChaosWarlords.Source.Core.Data.Dtos.ScoreBreakdownDto GetScoreBreakdown(Player player, MatchContext context);

        /// <summary>
        /// Determines the winner based on final scores.
        /// </summary>
        /// <param name="players">All players in the match.</param>
        /// <param name="context">The match context.</param>
        /// <returns>The player with the highest score.</returns>
        Player DetermineWinner(List<Player> players, MatchContext context);
    }
}
