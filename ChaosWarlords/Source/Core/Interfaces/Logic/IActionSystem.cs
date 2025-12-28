using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Interfaces.Logic
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
        void TryStartDevourHand(Card sourceCard);

        // Dependency Injection Setter (to avoid circular constructor dependencies if necessary, usually optional)
        void SetPlayerStateManager(IPlayerStateManager stateManager);
    }
}



