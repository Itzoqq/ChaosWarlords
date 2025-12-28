using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States;

namespace ChaosWarlords.Source.Commands
{
    public class ToggleMarketCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("ToggleMarket", state.IsMarketOpen ? "Closed Market" : "Opened Market");
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


