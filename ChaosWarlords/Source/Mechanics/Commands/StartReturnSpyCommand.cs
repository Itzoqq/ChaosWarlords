using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class StartReturnSpyCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.ActionSystem.TryStartReturnSpy();
            if (state.ActionSystem.CurrentState == ActionState.TargetingReturnSpy)
            {
                state.SwitchToTargetingMode();
            }
        }
    }
}
