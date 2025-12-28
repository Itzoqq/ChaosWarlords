using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Commands
{
    public class BuyCardCommand : IGameCommand
    {
        private readonly Card _card;
        public BuyCardCommand(Card card) { _card = card; }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("BuyCard", $"Bought {_card.Name}");
            // Accessing via interface properties
            state.MarketManager.TryBuyCard(state.TurnManager.ActivePlayer, _card, state.MatchContext.PlayerStateManager);
        }
    }
}



