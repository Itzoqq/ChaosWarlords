using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class ToggleMarketCommand : IGameCommand
    {
        public bool Validate(MatchContext context)
        {
            return true;
        }

        public void Execute(MatchContext context)
        {
             if (context.MarketManager is ChaosWarlords.Source.Core.Interfaces.Services.IMarketStateManager mgr)
             {
                 if (mgr.IsOpen) mgr.Close();
                 else mgr.OpenForBrowsing();
             }
        }
    }
}
