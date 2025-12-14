using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

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

    public class MapNodeClickedCommand : IGameCommand
    {
        private readonly MapNode _node;
        public MapNodeClickedCommand(MapNode node) { _node = node; }

        public void Execute(GameplayState state)
        {
            if (state._actionSystem.IsTargeting())
            {
                // Logic: If clicking a node while targeting, we might actually mean the Site it belongs to.
                Site site = state._mapManager.GetSiteForNode(_node);
                bool success = state._actionSystem.HandleTargetClick(_node, site);

                if (success) state.OnActionCompleted();
            }
            else
            {
                state._mapManager.TryDeploy(state._activePlayer, _node);
            }
        }
    }

    public class SiteClickedCommand : IGameCommand
    {
        private readonly Site _site;
        public SiteClickedCommand(Site site) { _site = site; }

        public void Execute(GameplayState state)
        {
            if (state._actionSystem.IsTargeting())
            {
                // Pass null for node, but valid site
                bool success = state._actionSystem.HandleTargetClick(null, _site);
                if (success) state.OnActionCompleted();
            }
        }
    }

    public class StartAssassinateCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            state._actionSystem.TryStartAssassinate();
        }
    }

    public class StartReturnSpyCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            state._actionSystem.TryStartReturnSpy();
        }
    }

    // Needed for the Spy Selection Popup
    public class ResolveSpyCommand : IGameCommand
    {
        private readonly PlayerColor _spyColor;
        public ResolveSpyCommand(PlayerColor spyColor) { _spyColor = spyColor; }

        public void Execute(GameplayState state)
        {
            if (state._actionSystem.FinalizeSpyReturn(_spyColor))
            {
                state.OnActionCompleted();
            }
        }
    }

    public class CancelActionCommand : IGameCommand
    {
        public void Execute(GameplayState state) { state._actionSystem.CancelTargeting(); }
    }
}