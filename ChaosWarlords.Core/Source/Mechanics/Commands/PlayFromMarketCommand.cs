using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Plays a card in the market "as if it was in your hand" (e.g. Ulitharid), then devours
    /// it - see MatchManager.PlayCardFromMarket for the actual orchestration. The source card
    /// (Ulitharid itself) is read from ActionSystem.PendingCard at Execute() time rather than
    /// carried on this command/DTO - it's already correctly set by TryStartPlayFromMarket and
    /// restored via the same StateRestorer/replay path every other pending-targeting field
    /// uses, so there's no need to duplicate it here.
    /// </summary>
    public class PlayFromMarketCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.PlayFromMarket;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.PlayFromMarketCommandDto
            {
                MarketCardRuntimeId = MarketCardRuntimeId,
                MarketCardId = MarketCardId
            };
        }

        public System.Guid MarketCardRuntimeId { get; }
        public string MarketCardId { get; }

        public PlayFromMarketCommand(Card marketCard, Card sourceCard)
        {
            MarketCardRuntimeId = marketCard.RuntimeId;
            MarketCardId = marketCard.Id;
        }

        public PlayFromMarketCommand(System.Guid marketCardRuntimeId, string marketCardId)
        {
            MarketCardRuntimeId = marketCardRuntimeId;
            MarketCardId = marketCardId;
        }

        private static Card? ResolveMarketCard(MatchContext context, System.Guid runtimeId)
        {
            return context.MarketManager.MarketRow?.FirstOrDefault(c => c.RuntimeId == runtimeId);
        }

        public bool Validate(MatchContext context)
        {
            var marketCard = ResolveMarketCard(context, MarketCardRuntimeId);
            if (marketCard == null) return false;

            var sourceCard = context.ActionSystem.PendingCard;
            if (sourceCard == null) return false;

            int maxCost = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.PlayFromMarket)?.Amount ?? 0;
            return marketCard.Cost <= maxCost;
        }

        public void Execute(MatchContext context)
        {
            var marketCard = ResolveMarketCard(context, MarketCardRuntimeId);
            var sourceCard = context.ActionSystem.PendingCard;
            if (marketCard == null || sourceCard == null) return;

            // Resolve/pop THIS effect (the "which market card" selection) first, matching
            // every other targeting command's own CompleteAction() call - the market card's
            // own effect resolution (PlayCardFromMarket -> CardEffectProcessor.ResolveEffects)
            // is a separate, independent stack sequence pushed fresh onto the now-empty
            // stack, not nested inside this still-pending one. Calling PlayCardFromMarket
            // first would leave this EffectContext dangling underneath the market card's own
            // pushed effects, so the stack would never fully drain and OnActionCompleted would
            // never fire for either sequence.
            context.ActionSystem.CompleteAction();
            context.MatchManager.PlayCardFromMarket(marketCard, sourceCard);
        }
    }
}
