using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.BuyCard;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.BuyCardCommandDto
            {
                CardId = CardId,
                CardRuntimeId = CardRuntimeId
            };
        }

        /// <summary>
        /// The specific market card copy to buy. Resolved against the market row in
        /// Validate()/Execute() - matches the ID-based pattern the other commands use.
        /// </summary>
        public System.Guid CardRuntimeId { get; }
        public string CardId { get; }

        public BuyCardCommand(Card card)
        {
            CardRuntimeId = card.RuntimeId;
            CardId = card.Id;
        }

        public BuyCardCommand(System.Guid cardRuntimeId, string cardId)
        {
            CardRuntimeId = cardRuntimeId;
            CardId = cardId;
        }

        private Card? ResolveCard(MatchContext context) =>
            context.MarketManager.MarketRow?.FirstOrDefault(c => c.RuntimeId == CardRuntimeId);

        public bool Validate(MatchContext context)
        {
            // Valid if card is in market and player checks out
            // NOTE: Simple validation here. Deep validation logic is inside MarketManager.TryBuyCard usually.
            // But we can check basic state.
            return ResolveCard(context) != null;
        }

        public void Execute(MatchContext context)
        {
            var card = ResolveCard(context);
            if (card != null)
            {
                context.MarketManager.TryBuyCard(context.TurnManager.ActivePlayer, card, context.PlayerStateManager);
            }
        }
    }
}
