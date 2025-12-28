using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Listens to GameEvents and logs them using the centralized GameLogger.
    /// adheres to Single Responsibility Principle by decoupling logging logic from game logic.
    /// </summary>
    public class GameEventLogger
    {
        private readonly IEventManager _eventManager;

        public GameEventLogger(IEventManager eventManager)
        {
            _eventManager = eventManager;
        }

        public void Initialize()
        {
            _eventManager.Subscribe<StateChangeEvent>(OnStateChanged);
            _eventManager.Subscribe<GameEvent>(OnGenericEvent);
        }

        public void Cleanup()
        {
            _eventManager.Unsubscribe<StateChangeEvent>(OnStateChanged);
            _eventManager.Unsubscribe<GameEvent>(OnGenericEvent);
        }

        private void OnStateChanged(StateChangeEvent evt)
        {
            GameLogger.Log($"[State] {evt.StateName}: {evt.OldValue} -> {evt.NewValue}", LogChannel.Info);
        }

        private void OnGenericEvent(GameEvent evt)
        {
            // Avoid double logging StateChangeEvents since they inherit from GameEvent
            if (evt is StateChangeEvent) return;

            GameLogger.Log($"[Event] {evt.Context}", LogChannel.Debug);
        }
    }
}
