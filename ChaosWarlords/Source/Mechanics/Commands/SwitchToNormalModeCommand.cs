using ChaosWarlords.Source.Core.Interfaces.State;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Executes a switch back to normal input mode. Used to break out of incorrect input modes.
    /// </summary>
    public class SwitchToNormalModeCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("SwitchToNormal", "Switched back to normal play mode");
            state.SwitchToNormalMode();
        }
    }
}


