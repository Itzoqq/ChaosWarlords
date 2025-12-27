using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class StartAssassinateCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.ActionSystem.TryStartAssassinate();
            if (state.ActionSystem.CurrentState == ActionState.TargetingAssassinate)
            {
                state.SwitchToTargetingMode();
            }
        }
    }
}


