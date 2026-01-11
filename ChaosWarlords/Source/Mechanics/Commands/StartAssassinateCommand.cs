using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts; // Correct namespace for MatchContext

namespace ChaosWarlords.Source.Commands
{
    public class StartAssassinateCommand : IGameCommand
    {
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
            context.ActionSystem.StartTargeting(ChaosWarlords.Source.Utilities.ActionState.TargetingAssassinate, null);
        }
    }
}


