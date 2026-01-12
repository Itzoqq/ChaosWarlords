using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class ToggleMarketCommand : IGameCommand
    {
        public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.ToggleMarket;

        public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new ChaosWarlords.Source.Core.Data.Dtos.ToggleMarketCommandDto();
        }
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
