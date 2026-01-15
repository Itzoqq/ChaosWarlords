using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Executes a switch back to normal input mode. Used to break out of incorrect input modes.
    /// </summary>
    public class SwitchToNormalModeCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.SwitchMode;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.SwitchModeCommandDto();
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
