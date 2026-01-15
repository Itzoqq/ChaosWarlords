using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class ToggleMarketCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ToggleMarket;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ToggleMarketCommandDto();
        }
        public bool Validate(MatchContext context)
        {
            return true;
        }

        public void Execute(MatchContext context)
        {
            if (context.MarketManager is Core.Interfaces.Services.IMarketStateManager mgr)
            {
                if (mgr.IsOpen) mgr.Close();
                else mgr.OpenForBrowsing();
            }
        }
    }
}
