using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;

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


