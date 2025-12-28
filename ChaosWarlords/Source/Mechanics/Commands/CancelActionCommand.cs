using ChaosWarlords.Source.Core.Interfaces.State;

namespace ChaosWarlords.Source.Commands
{
    public class CancelActionCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("CancelAction", "Cancelled current action");
            state.ActionSystem.CancelTargeting();
            state.SwitchToNormalMode();
        }
    }
}


