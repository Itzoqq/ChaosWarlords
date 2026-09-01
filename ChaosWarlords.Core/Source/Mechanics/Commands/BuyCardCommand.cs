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
            var card = ResolveCard(context);
            if (card == null) return false;

            // Every other resource-gated command enforces its own cost precondition
            // directly in Validate() (AssassinateCommand's Power, PlaceSpyCommand's
            // SpiesInBarracks, SupplantCommand's TroopsInBarracks) - this used to be the
            // one exception, letting an insufficient-funds purchase pass Validate()
            // (advancing SequenceNumber, getting recorded) and rely entirely on
            // MarketManager.TryBuyCard's own internal guard to silently no-op it. See
            // planning.txt TIER 1 (test hardening audit, 2026-09-01).
            return context.TurnManager.ActivePlayer.Influence >= card.Cost;
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
