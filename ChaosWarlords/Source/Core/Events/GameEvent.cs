using System;

namespace ChaosWarlords.Source.Core.Events
{
    /// <summary>
    /// Abstract base record for all game events.
    /// Supports the Event-Driven Architecture.
    /// </summary>
    public abstract record GameEvent
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public Guid? SourceId { get; init; }
        public string Context { get; init; } = "General";
    }
}
