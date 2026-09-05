using ChaosWarlords.Source.Utilities;
using System.Text.Json.Serialization;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Represents a single item in the ActionSystem execution stack.
    /// Used for saving/restoring mid-action state (e.g. while targeting).
    /// </summary>
    public class EffectContextDto
    {
        public ActionState State { get; set; }
        public string? SourceCardId { get; set; }
        public bool RequiresInput { get; set; }
        public string Description { get; set; } = string.Empty;
        
        // We might need to serialize the embedded SourceEffect too if deep resumption is needed,
        // but typically looking up the effect from the SourceCard's definitions is safer 
        // than serializing the rules object itself.
        // For now, we rely on SourceCardId + State to infer context.
        // However, if we have nested effects (Devour -> Deploy), we might need an index or explicit EffectType.
        
        public EffectType EffectType { get; set; }

        /// <summary>
        /// Mirrors EffectContext.RemainingRepeats (see its own doc comment) - defaults to 1 so
        /// older/other effects round-trip unaffected. Without this, a snapshot/restore mid-way
        /// through a repeat effect (e.g. CommandDispatcher's rollback-on-exception after the
        /// first of Deathblade's 2 Assassinates) would silently lose track of how many targets
        /// were still owed.
        /// </summary>
        public int RemainingRepeats { get; set; } = 1;
    }
}
