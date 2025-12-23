using System;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public interface IActionSystem
    {
        event EventHandler OnActionCompleted;
        event EventHandler<string> OnActionFailed;

        ActionState CurrentState { get; }
        Card PendingCard { get; }
        Site PendingSite { get; }
        MapNode PendingMoveSource { get; }

        void TryStartAssassinate();
        void TryStartReturnSpy();
        void StartTargeting(ActionState state, Card card = null);
        void CancelTargeting();
        bool IsTargeting();

        void CompleteAction();

        void HandleTargetClick(MapNode targetNode, Site targetSite);
        void FinalizeSpyReturn(PlayerColor selectedSpyColor);
    }
}