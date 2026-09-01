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
        /// Explicitly sets the pending devour card (deferral).
        /// </summary>
        void DeferDevour(Card card);



        /// <summary>
        /// Handles the selection of a card to devour from Hand.
        /// </summary>
        Commands.DevourCardCommand? HandleDevourSelection(Card? targetCard);

        /// <summary>
        /// Handles the selection of a card to devour from Market.
        /// </summary>
        Commands.DevourCardCommand? HandleDevourMarketSelection(Card? targetCard);

        /// <summary>
        /// Clears the pending devour state (if any).
        /// </summary>
        void ClearState();

        // SetMatchManager/SetMarketStateManager stay setter-injected rather than constructor
        // params - genuine circular dependency, not an oversight: both arrive later, from the
        // CLIENT layer (GameplayState.cs), only after MatchContext/MatchManager/
        // MarketStateManager exist, which themselves need ActionSystem (and therefore this
        // subsystem) to already exist first. IPlayerStateManager/IMarketManager used to be
        // setters here too, but both are actually available at construction time (see
        // MatchFactory.SetupActionSystem) - promoted to required constructor params instead.
        void SetMatchManager(IMatchManager matchManager);
        void SetMarketStateManager(IMarketStateManager stateManager);
    }
}
