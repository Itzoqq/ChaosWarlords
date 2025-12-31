using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Manages victory conditions and final scoring calculations.
    /// Implements the complete end-game logic including all VP sources.
    /// </summary>
    public class VictoryManager : IVictoryManager
    {
        private readonly IGameLogger _logger;

        public VictoryManager(IGameLogger logger)
        {
            _logger = logger;
        }

        public bool CheckEndGameConditions(MatchContext context, out string reason)
        {
            reason = string.Empty;

            // Check 1: Any player out of troops?
            bool anyPlayerOutOfTroops = context.TurnManager.Players
                .Any(p => p.TroopsInBarracks == 0);

            // Check 2: Market deck empty?
            bool marketDeckEmpty = context.MarketManager.MarketRow.Count == 0 && 
                                   !context.MarketManager.HasCardsInDeck();

            if (anyPlayerOutOfTroops)
            {
                var player = context.TurnManager.Players.First(p => p.TroopsInBarracks == 0);
                reason = $"{player.DisplayName} has deployed their last troop!";
                _logger.Log($"End-game triggered: {reason}", LogChannel.Info);
                return true;
            }

            if (marketDeckEmpty)
            {
                reason = "Market deck is empty!";
                _logger.Log($"End-game triggered: {reason}", LogChannel.Info);
                return true;
            }

            return false;
        }

        public int CalculateFinalScore(Player player, MatchContext context)
        {
            var breakdown = GetScoreBreakdown(player, context);
            
            _logger.Log($"{player.DisplayName} Final Score: {breakdown.TotalScore} " +
                       $"(VP:{breakdown.VPTokens} Sites:{breakdown.SiteControlVP} Trophies:{breakdown.TrophyHallVP} " +
                       $"Deck:{breakdown.DeckVP} InnerCircle:{breakdown.InnerCircleVP})",
                       LogChannel.Info);

            return breakdown.TotalScore;
        }

        public ChaosWarlords.Source.Core.Data.Dtos.ScoreBreakdownDto GetScoreBreakdown(Player player, MatchContext context)
        {
            int vpTokens = player.VictoryPoints;
            int siteControl = CalculateSiteControlVP(player, context);
            int trophyHall = player.TrophyHall;
            int deckVP = CalculateDeckVP(player);
            int innerCircleVP = CalculateInnerCircleVP(player);

            return new ChaosWarlords.Source.Core.Data.Dtos.ScoreBreakdownDto
            {
                VPTokens = vpTokens,
                SiteControlVP = siteControl,
                TrophyHallVP = trophyHall,
                DeckVP = deckVP,
                InnerCircleVP = innerCircleVP,
                TotalScore = vpTokens + siteControl + trophyHall + deckVP + innerCircleVP
            };
        }

        private static int CalculateSiteControlVP(Player player, MatchContext context)
        {
            int vp = 0;

            foreach (var site in context.MapManager.Sites)
            {
                if (site.Owner == player.Color)
                {
                    vp += 1; // 1 VP per site controlled

                    // Check for total control (2 VP bonus)
                    // Total control = all troop spaces filled by this player, no enemy spies
                    if (site.HasTotalControl)
                    {
                        vp += 2;
                    }
                }
            }

            return vp;
        }

        private static int CalculateDeckVP(Player player)
        {
            int vp = 0;

            // Cards in deck
            foreach (var card in player.Deck)
                vp += card.DeckVP;

            // Cards in hand
            foreach (var card in player.Hand)
                vp += card.DeckVP;

            // Cards in discard
            foreach (var card in player.DiscardPile)
                vp += card.DeckVP;

            // Cards in played area
            foreach (var card in player.PlayedCards)
                vp += card.DeckVP;

            return vp;
        }

        private static int CalculateInnerCircleVP(Player player)
        {
            int vp = 0;

            foreach (var card in player.InnerCircle)
                vp += card.InnerCircleVP;

            return vp;
        }

        public Player DetermineWinner(List<Player> players, MatchContext context)
        {
            var scores = players.ToDictionary(
                p => p,
                p => CalculateFinalScore(p, context)
            );

            var winner = scores.OrderByDescending(kvp => kvp.Value).First().Key;
            
            _logger.Log($"Winner: {winner.DisplayName} with {scores[winner]} VP!", LogChannel.General);
            
            return winner;
        }
    }
}
