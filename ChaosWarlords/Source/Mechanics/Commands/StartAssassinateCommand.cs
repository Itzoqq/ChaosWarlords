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
