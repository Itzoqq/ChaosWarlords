using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public interface IActionSystem
    {
        ActionState CurrentState { get; }
        Card PendingCard { get; }
        Site PendingSite { get; }

        void SetCurrentPlayer(Player newPlayer);
        void TryStartAssassinate();
        void TryStartReturnSpy();
        void StartTargeting(ActionState state, Card card = null);
        void CancelTargeting();
        bool IsTargeting();
        bool HandleTargetClick(MapNode targetNode, Site targetSite);
        bool FinalizeSpyReturn(PlayerColor selectedSpyColor);
    }
}