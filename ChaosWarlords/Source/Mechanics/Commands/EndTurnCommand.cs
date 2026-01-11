using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class EndTurnCommand : IGameCommand
    {
        public bool Validate(MatchContext context)
        {
             // Check if can end turn (ActionPoints, etc)
             return true; 
        }

        public void Execute(MatchContext context)
        {
             context.MatchManager.EndTurn();
        }
    }
}
