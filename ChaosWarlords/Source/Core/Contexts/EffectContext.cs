using System;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Contexts
{
    /// <summary>
    /// Represents a single pending effect in the ActionSystem's execution stack.
    /// Used to handle complex, nested, or asynchronous card effects (e.g. "Assassinate then Promote").
    /// </summary>
    public class EffectContext
    {
        /// <summary>
        /// The type of effect to be resolved (e.g. Assassinate, ReturnSpy, Supplant).
        /// This corresponds to the ActionState or high-level intent.
        /// </summary>
        public ActionState EffectType { get; }

        /// <summary>
        /// The card that originated this effect.
        /// </summary>
        public Card SourceCard { get; }

        /// <summary>
        /// Any parameters required for the effect (e.g., valid targets, cost overrides).
        /// </summary>
        public object[] Parameters { get; }

        /// <summary>
        /// Callback action to execute when the effect is successfully resolved.
        /// The bool parameter indicates success/failure if needed.
        /// </summary>
        public Action<bool> OnResolved { get; }

        /// <summary>
        /// Callback action to execute if the effect is cancelled or fails.
        /// </summary>
        public Action? OnCancelled { get; }

        /// <summary>
        /// Does this effect require user input (blocking)?
        /// If true, the system waits for a command. If false, it executes immediately.
        /// </summary>
        public bool RequiresInput { get; }

        /// <summary>
        /// Debug description of the effect.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The specific effect definition from the card (optional).
        /// </summary>
        public ChaosWarlords.Source.Entities.Cards.CardEffect? SourceEffect { get; }

        public EffectContext(
            ActionState effectType, 
            Card sourceCard, 
            bool requiresInput, 
            string description,
            Action<bool> onResolved,
            ChaosWarlords.Source.Entities.Cards.CardEffect? sourceEffect = null,
            Action? onCancelled = null,
            params object[] parameters)
        {
            EffectType = effectType;
            SourceCard = sourceCard;
            RequiresInput = requiresInput;
            Description = description;
            OnResolved = onResolved;
            SourceEffect = sourceEffect;
            OnCancelled = onCancelled;
            Parameters = parameters;
        }
    }
}
