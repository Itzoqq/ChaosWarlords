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
    /// Strategy for devouring the card itself (Self-Devour).
    /// </summary>
    internal class DevourSelfStrategy : IDevourStrategy
    {
        public void Execute(Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer)
        {
            // Guard against being invoked twice for the same card (e.g. a stray duplicate
            // accept-click reaching this a second time) - CardsMarkedForTurnEndDevour is a
            // plain List<Card>, and MatchManager.EndTurn's cleanup loop isn't itself
            // duplicate-safe: a second entry for the same card would add it to VoidPile
            // twice. See UIManager.IsConfirmationPopupVisible's doc comment for the double-
            // click bug this was actually observed under (now fixed at the source), kept
            // here as defense in depth.
            if (context.CardsMarkedForTurnEndDevour.Contains(sourceCard))
            {
                onComplete?.Invoke();
                return;
            }

            logger.Log($"{sourceCard.Name}: Marked for self-devour at end of turn.", LogChannel.Info);

            // Mark for end-of-turn destruction
            context.CardsMarkedForTurnEndDevour.Add(sourceCard);

            // Execute the 'Success' effect immediately
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Strategy for devouring cards from the Inner Circle.
    /// </summary>
    internal class DevourFromInnerCircleStrategy : IDevourStrategy
    {
        public void Execute(Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer)
        {
            context.ActionSystem.TryStartDevourInnerCircle(sourceCard, onComplete, defer);
        }
    }

    /// <summary>
    /// Factory for creating appropriate devour strategy based on card location.
    /// </summary>
    internal static class DevourStrategyFactory
    {
        private static readonly DevourFromHandStrategy _handStrategy = new();
        private static readonly DevourFromMarketStrategy _marketStrategy = new();
        private static readonly DevourSelfStrategy _selfStrategy = new();
        private static readonly DevourFromInnerCircleStrategy _innerCircleStrategy = new();

        public static IDevourStrategy GetStrategy(CardLocation location)
        {
            return location switch
            {
                CardLocation.Market => _marketStrategy,
                CardLocation.Hand => _handStrategy,
                CardLocation.Self => _selfStrategy,
                CardLocation.InnerCircle => _innerCircleStrategy,
                _ => _handStrategy // Default fallback to Hand
            };
        }
    }
}
