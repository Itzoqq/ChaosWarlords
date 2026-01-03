using ChaosWarlords.Source.Entities.Cards;
using System;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for UI event mediation between game logic and UI layer.
    /// Allows game logic to request UI interactions without direct coupling.
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
