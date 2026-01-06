using System;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Strategy interface for handling different devour locations.
    /// </summary>
    internal interface IDevourStrategy
    {
        void Execute(Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer);
    }

    /// <summary>
    /// Strategy for devouring cards from the player's hand.
    /// </summary>
    internal class DevourFromHandStrategy : IDevourStrategy
    {
        public void Execute(Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer)
        {
            if (context.ActivePlayer.Hand.Count > 0)
            {
                context.ActionSystem.TryStartDevourHand(sourceCard, onComplete, defer);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: Hand empty, cannot Devour.", LogChannel.Warning);
            }
        }
    }

    /// <summary>
    /// Strategy for devouring cards from the market.
    /// </summary>
    internal class DevourFromMarketStrategy : IDevourStrategy
    {
        public void Execute(Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer)
        {
            context.ActionSystem.TryStartDevourMarket(sourceCard, onComplete, defer);
        }
    }

    /// <summary>
    /// Factory for creating appropriate devour strategy based on card location.
    /// </summary>
    internal static class DevourStrategyFactory
    {
        private static readonly DevourFromHandStrategy _handStrategy = new();
        private static readonly DevourFromMarketStrategy _marketStrategy = new();

        public static IDevourStrategy GetStrategy(CardLocation location)
        {
            return location switch
            {
                CardLocation.Market => _marketStrategy,
                CardLocation.Hand => _handStrategy,
                _ => _handStrategy // Default fallback to Hand
            };
        }
    }
}
