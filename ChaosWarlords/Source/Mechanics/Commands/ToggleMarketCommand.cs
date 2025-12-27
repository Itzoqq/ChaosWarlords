using ChaosWarlords.Source.States;

namespace ChaosWarlords.Source.Commands
{
    public class ToggleMarketCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            // Don't just flip the boolean. 
            // Call the methods that handle the State Transition logic.

            if (state.IsMarketOpen)
            {
                state.CloseMarket(); // This sets IsMarketOpen=false AND switches to NormalPlayInputMode
            }
            else
            {
                state.ToggleMarket(); // This sets IsMarketOpen=true AND switches to MarketInputMode
            }
        }
    }
}
