using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;

namespace ChaosWarlords.Source.Commands
{
    public class ToggleMarketCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("ToggleMarket", state.IsMarketOpen ? "Closed Market" : "Opened Market");
            // Don't just flip the boolean. 
            if (state.MarketStateManager.IsOpen)
            {
                state.MarketStateManager.Close();
            }
            else
            {
                state.MarketStateManager.OpenForBrowsing();
            }
        }
    }
}
