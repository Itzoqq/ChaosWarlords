using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    public interface ISpySubsystem
    {
        IGameCommand? HandlePlaceSpy(Site targetSite, string? cardId);
        IGameCommand? HandleReturnSpyInitialClick(Site clickedSite, string? cardId);
        IGameCommand? FinalizeSpyReturn(PlayerColor selectedSpyColor, Site pendingSite, string? cardId);
        
        bool PerformSpyReturn(Site site, PlayerColor selectedSpyColor, string? cardId);
        void PerformPlaceSpy(Site site, string? cardId);
    }
}
