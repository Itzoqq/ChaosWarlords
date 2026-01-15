using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Command executed after a successful card action or targeting phase is complete.
    /// It finalizes the card play and resets the action system.
    /// </summary>
    public class ActionCompletedCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ActionCompleted;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ActionCompletedCommandDto();
        }
        public bool Validate(MatchContext context)
        {
            return true;
        }

        public void Execute(MatchContext context)
        {
            // Just a marker command, no logic needed
        }
    }
}
