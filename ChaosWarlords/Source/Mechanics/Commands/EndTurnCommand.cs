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
                // Use state.EndTurn() which handles cleanup (mode switch, cancel targeting)
                // MatchManager.EndTurn() is part of state.EndTurn()
                state.EndTurn();
            }
            else
            {
                state.Logger.Log(reason, LogChannel.Warning);
            }
        }
    }
}


