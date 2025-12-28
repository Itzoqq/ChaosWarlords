using ChaosWarlords.Source.Core.Events;

namespace ChaosWarlords.Source.Core.Events
{
    /// <summary>
    /// Event fired when a significant state change occurs in the game.
    /// Helpful for debugging and replay logs.
    /// </summary>
    public record StateChangeEvent : GameEvent
    {
        public string StateName { get; init; }
        public string NewValue { get; init; }
        public string OldValue { get; init; }

        public StateChangeEvent(string stateName, object newValue, object oldValue = null)
        {
            StateName = stateName;
            NewValue = newValue?.ToString() ?? "null";
            OldValue = oldValue?.ToString() ?? "null";
            Context = "StateChange";
        }
    }
}
