using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts; // Correct namespace for MatchContext

namespace ChaosWarlords.Source.Commands
{
    public class StartAssassinateCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.StartAssassinate;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.StartAssassinateCommandDto();
        }
        public bool Validate(MatchContext context)
        {
            return true;
        }

        public void Execute(MatchContext context)
        {
            // Switching modes is strictly State/View related?
            // Actually, "Targeting Mode" change IS a change in Game State (Input State).
            // But we moved Input logic out of MatchContext?
            // "ActionState" is in ActionSystem (which is in MatchContext).

            // So we start targeting via ActionSystem.
            context.ActionSystem.StartTargeting(ActionState.TargetingAssassinate, null);
        }
    }
}


