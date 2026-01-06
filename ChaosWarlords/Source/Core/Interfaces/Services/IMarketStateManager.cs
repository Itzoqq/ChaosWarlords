using System;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Manages the state and mode of market interactions.
    /// Centralizes all market-related state transitions and provides events for UI updates.
    /// </summary>
    public interface IMarketStateManager
    {
        /// <summary>
        /// Gets the current market interaction mode.
        /// </summary>
        MarketMode CurrentMode { get; }
        
        /// <summary>
        /// Gets whether the market is currently open (any mode except Closed).
        /// </summary>
        bool IsOpen { get; }
        
        /// <summary>
        /// Gets the callback for devour targeting mode, if active.
        /// </summary>
        Func<Card, ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand?>? DevourCallback { get; }
        
        /// <summary>
        /// Opens the market in browsing/buying mode.
        /// </summary>
        void OpenForBrowsing();
        
        /// <summary>
        /// Opens the market in devour targeting mode with the specified callback.
        /// </summary>
        /// <param name="onDevourCallback">Callback to invoke when a card is selected for devouring</param>
        void OpenForDevour(Func<Card, ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand?> onDevourCallback);
        
        /// <summary>
        /// Closes the market.
        /// </summary>
        void Close();
        
        /// <summary>
        /// Event raised when the market mode changes.
        /// </summary>
        event EventHandler<MarketMode>? ModeChanged;
    }
}
