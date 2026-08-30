using System;
using ChaosWarlords.Source.Core.Events;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Manages the publication and subscription of game events.
    /// Decouples event producers from consumers.
    /// </summary>
    public interface IEventManager
    {
        /// <summary>
        /// Publishes an event to all subscribers of its type.
        /// </summary>
        /// <param name="gameEvent">The event to publish.</param>
        void Publish(GameEvent gameEvent);

        /// <summary>
        /// Subscribes a handler to a specific event type.
        /// </summary>
        /// <typeparam name="T">The type of GameEvent to subscribe to.</typeparam>
        /// <param name="handler">The action to execute when the event occurs.</param>
        void Subscribe<T>(Action<T> handler) where T : GameEvent;

        /// <summary>
        /// Unsubscribes a handler from a specific event type.
        /// </summary>
        /// <typeparam name="T">The type of GameEvent to unsubscribe from.</typeparam>
        /// <param name="handler">The handler to remove.</param>
        void Unsubscribe<T>(Action<T> handler) where T : GameEvent;
    }
}
