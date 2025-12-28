using System;

namespace ChaosWarlords.Source.Contexts
{
    /// <summary>
    /// Represents a single recorded action within a turn for determinism and replay.
    /// </summary>
    public record ExecutedAction(
        int Sequence,
        string ActionType,
        Guid PlayerId,
        string Summary,
        DateTime Timestamp
    );
}
