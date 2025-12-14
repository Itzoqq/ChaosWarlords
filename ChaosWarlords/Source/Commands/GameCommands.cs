using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Source.Commands
{
    public interface IGameCommand
    {
        void Execute(GameplayState state);
    }

    public class BuyCardCommand : IGameCommand
    {
        private readonly Card _card;
        public BuyCardCommand(Card card) { _card = card; }

        public void Execute(GameplayState state)
        {
            state._marketManager.TryBuyCard(state._activePlayer, _card);
        }
    }

    public class DeployTroopCommand : IGameCommand
    {
        private readonly MapNode _node;
        public DeployTroopCommand(MapNode node) { _node = node; }

        public void Execute(GameplayState state)
        {
            state._mapManager.TryDeploy(state._activePlayer, _node);
        }
    }

    public class ToggleMarketCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            // Toggles the boolean flag in GameplayState
            state._isMarketOpen = !state._isMarketOpen;
        }
    }

    public class PlayCardCommand : IGameCommand
    {
        private readonly Card _card;
        public PlayCardCommand(Card card) { _card = card; }

        public void Execute(GameplayState state)
        {
            state.PlayCard(_card);
        }
    }
}