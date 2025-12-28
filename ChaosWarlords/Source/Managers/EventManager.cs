using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Concrete implementation of the Event System.
    /// Uses a Dictionary to map Event Types to Delegate lists.
    /// </summary>
    public class EventManager : IEventManager
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        public void Publish(GameEvent gameEvent)
        {
            if (gameEvent == null) return;

            var eventType = gameEvent.GetType();
            List<Delegate>? handlers = null;

            lock (_lock)
            {
                if (_subscribers.TryGetValue(eventType, out var existingHandlers))
                {
                    // Create a copy to allow modification during iteration (e.g., recursive events or unsubscription)
                    handlers = new List<Delegate>(existingHandlers);
                }
            }

            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler.DynamicInvoke(gameEvent);
                    }
                    catch (Exception ex)
                    {
                        // In a real game, log this error instead of swallowing it.
                        // Assuming GameLogger is available via static access, or we might inject it later.
                        System.Diagnostics.Debug.WriteLine($"Error handling event {eventType.Name}: {ex}");
                    }
                }
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null) return;

            lock (_lock)
            {
                var eventType = typeof(T);
                if (!_subscribers.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscribers[eventType] = handlers;
                }
                handlers.Add(handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null) return;

            lock (_lock)
            {
                var eventType = typeof(T);
                if (_subscribers.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        _subscribers.Remove(eventType);
                    }
                }
            }
        }
    }
}
