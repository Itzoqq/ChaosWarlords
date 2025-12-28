using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class EndTurnCommand : IGameCommand
    {
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("EndTurn", "Ended turn");
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


