using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class CancelActionCommand : IGameCommand
    {
        public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.CancelAction;

        public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new ChaosWarlords.Source.Core.Data.Dtos.CancelActionCommandDto();
        }
        public bool Validate(MatchContext context)
        {
            return true;
        }

        public void Execute(MatchContext context)
        {
            context.ActionSystem.CancelTargeting();
        }
    }
}
