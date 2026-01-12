using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.BuyCard;

        public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new ChaosWarlords.Source.Core.Data.Dtos.BuyCardCommandDto
            {
                CardId = Card.Id
            };
        }
        public Card Card { get; }
        public BuyCardCommand(Card card) { Card = card; }

        public bool Validate(MatchContext context)
        {
             // Valid if card is in market and player checks out
             // NOTE: Simple validation here. Deep validation logic is inside MarketManager.TryBuyCard usually.
             // But we can check basic state.
             var player = context.TurnManager.ActivePlayer;
             return context.MarketManager.MarketRow.Contains(Card);
        }

        public void Execute(MatchContext context)
        {
            context.MarketManager.TryBuyCard(context.TurnManager.ActivePlayer, Card, context.PlayerStateManager);
        }
    }
}
