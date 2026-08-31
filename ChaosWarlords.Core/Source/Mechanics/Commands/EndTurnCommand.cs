using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class EndTurnCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.EndTurn;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.EndTurnCommandDto();
        }
        public bool Validate(MatchContext context)
        {
            // Always legal: per the rules a player may end their turn early at any time -
            // there's no "must spend everything first" requirement to check.
            return true;
        }

        public void Execute(MatchContext context)
        {
            context.MatchManager.EndTurn();
        }
    }
}
