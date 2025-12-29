using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        public Card Card { get; }
        public BuyCardCommand(Card card) { Card = card; }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("BuyCard", $"Bought {Card.Name}");
            // Accessing via interface properties
            state.MarketManager.TryBuyCard(state.TurnManager.ActivePlayer, Card, state.MatchContext!.PlayerStateManager);
        }
    }
}



