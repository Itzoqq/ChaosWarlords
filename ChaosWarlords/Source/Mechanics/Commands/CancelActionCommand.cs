using ChaosWarlords.Source.States;

namespace ChaosWarlords.Source.Commands
{
    public class CancelActionCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.ActionSystem.CancelTargeting();
            state.SwitchToNormalMode();
        }
    }
}
