using System;

namespace ChaosWarlords.Source.Core.Data.Recording
{
    /// <summary>
    /// Represents a recorded command execution.
    /// Used for Replays and Multiplayer synchronization.
    /// </summary>
    public class ReplayAction
    {
        public int Sequence { get; set; }
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// The fully qualified type name of the Command.
        /// </summary>
        public string CommandType { get; set; }
        
        /// <summary>
        /// JSON serialized serialization of the command DTO/Fields.
        /// </summary>
        public string CommandJson { get; set; }
    }
}
