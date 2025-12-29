using System;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// polymorphic-ish DTO for recording commands.
    /// We use a single flat DTO with optional fields to keep serialization simple,
    /// or we could use inheritance with type discriminators.
    /// Flat is often easier for simple JSON logs.
    /// </summary>
    public class CommandDto
    {
        public string CommandType { get; set; } = string.Empty; // e.g., "PlayCard", "BuyCard"
        public int SequenceNumber { get; set; }
        public Guid PlayerId { get; set; }
        
        // --- Payload Fields (Optional based on Type) ---
        
        // For Card Actions
        public string? CardDefinitionId { get; set; }
        public int? CardHandIndex { get; set; }
        public int? CardMarketIndex { get; set; }
        
        // For Map Actions
        public int? TargetNodeId { get; set; }
        public int? SourceNodeId { get; set; } // If we add movement later
        
        // For Generic Data
        public string? Context { get; set; }
    }
}
