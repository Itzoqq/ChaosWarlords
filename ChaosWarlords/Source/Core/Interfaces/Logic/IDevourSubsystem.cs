using System;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Source.Core.Interfaces.Logic
{
    public interface IDevourSubsystem
    {
        /// <summary>
        /// The card pending to be devoured (buffered).
        /// </summary>
        Card? PendingDevourCard { get; }

        /// <summary>
        /// Initiates the Devour Hand action flow.
        /// </summary>
        void TryStartDevourHand(Card sourceCard, Action? onComplete = null, bool deferExecution = false);

        /// <summary>
        /// Initiates the Devour Market action flow.
        /// </summary>
        void TryStartDevourMarket(Card sourceCard, Action? onComplete = null, bool deferExecution = false);

        /// <summary>
        /// Initiates the Devour Deck action flow.
        /// </summary>
        void TryStartDevourDeck(Card sourceCard, Action? onComplete = null, bool deferExecution = false);

        /// <summary>
        /// Handles the selection of a card to devour from Hand.
        /// </summary>
        void HandleDevourSelection(Card? targetCard);

        /// <summary>
        /// Handles the selection of a card to devour from Market.
        /// </summary>
        void HandleDevourMarketSelection(Card? targetCard);

        /// <summary>
        /// Clears the pending devour state (if any).
        /// </summary>
        void ClearState();

        void SetMatchManager(IMatchManager matchManager);
        void SetMarketManager(IMarketManager marketManager);
        void SetPlayerStateManager(IPlayerStateManager stateManager);
        void SetMarketStateManager(IMarketStateManager stateManager);
    }
}
