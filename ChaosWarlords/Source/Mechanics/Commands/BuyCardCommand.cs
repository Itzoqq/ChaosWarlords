using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        private readonly Card _card;
        public BuyCardCommand(Card card) { _card = card; }

        public void Execute(IGameplayState state)
        {
            // Accessing via interface properties
            state.MarketManager.TryBuyCard(state.TurnManager.ActivePlayer, _card);
        }
    }
}
