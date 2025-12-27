using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class EndTurnCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            // Validation Check
            if (state.CanEndTurn(out string reason))
            {
                state.EndTurn();
            }
            else
            {
                GameLogger.Log(reason, LogChannel.Warning);
            }
        }
    }
}
