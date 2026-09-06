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
            // Per the rules a player may end their turn early at any time - there's no
            // "must spend everything first" requirement to check - EXCEPT while a targeting
            // sequence is still in progress (including a deferred "up to N" promotion
            // redemption or a forced-discard flow): ending the turn out from under it would
            // desync/orphan state the UI is still actively presenting, and previously had no
            // command-layer defense at all (only a UI-layer button/CanEndTurn check, which any
            // other caller - AI, network client, replay - could bypass entirely). See
            // planning.txt.
            return !context.ActionSystem.IsTargeting();
        }

        public void Execute(MatchContext context)
        {
            context.MatchManager.EndTurn();
        }
    }
}
