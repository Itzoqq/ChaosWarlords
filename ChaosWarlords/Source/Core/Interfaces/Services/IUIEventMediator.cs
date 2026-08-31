using ChaosWarlords.Source.Entities.Cards;
using System;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for UI event mediation between game logic and UI layer.
    /// Allows game logic to request UI interactions without direct coupling.
    ///
    /// Lives in the CLIENT project, not ChaosWarlords.Core - it moved here 2026-08-31 once
    /// grep confirmed nothing in Core reads a MatchContext.UIEventMediator anymore (that
    /// property/constructor parameter was removed the same day). ActionSystem talks to the
    /// UI layer via IActionSystem.OnInteractionRequested (a plain event carrying
    /// InteractionRequest) instead of calling into this interface directly - see
    /// ActionSystem.cs and planning.txt. This interface is purely the client-side contract
    /// UIEventMediator implements to answer that event; headless logic has no reason to
    /// know it exists.
    /// </summary>
    public interface IUIEventMediator
    {
        /// <summary>
        /// Event raised when an optional card effect requires player choice.
        /// </summary>
        event Action<Card, CardEffect, Action, Action>? OnOptionalEffectRequested;

        /// <summary>
        /// Request player choice for an optional card effect.
        /// </summary>
        /// <param name="card">The card with the optional effect</param>
        /// <param name="effect">The optional effect to present</param>
        /// <param name="onAccept">Callback if player accepts</param>
        /// <param name="onDecline">Callback if player declines</param>
        void RequestOptionalEffect(Card card, CardEffect effect, Action onAccept, Action onDecline);
    }
}
