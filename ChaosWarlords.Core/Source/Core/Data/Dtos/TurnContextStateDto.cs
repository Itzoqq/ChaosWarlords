using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>In-memory checkpoint for all mutable state that belongs to one turn.</summary>
    public sealed class TurnContextStateDto
    {
        public Dictionary<CardAspect, int> PlayedAspectCounts { get; set; } = [];
        public List<PromotionCreditStateDto> PromotionCredits { get; set; } = [];
        public List<UnboundedPromotionCreditStateDto> PendingUnboundedCredits { get; set; } = [];
        public List<PromotionCompletionEffectStateDto> PromotionCompletionEffects { get; set; } = [];
        public int ActionSequence { get; set; }
        public List<ExecutedActionStateDto> ActionHistory { get; set; } = [];
    }

    public sealed class PromotionCreditStateDto
    {
        public Guid SourceCardRuntimeId { get; set; }
        public bool IsOptional { get; set; }
        public CardAspect? RequiredAspect { get; set; }
        public CardCreatureType? RequiredCreatureType { get; set; }
    }

    public sealed class UnboundedPromotionCreditStateDto
    {
        public Guid SourceCardRuntimeId { get; set; }
        public CardCreatureType? RequiredCreatureType { get; set; }
    }

    public sealed class PromotionCompletionEffectStateDto
    {
        public Guid SourceCardRuntimeId { get; set; }
        public EffectType EffectType { get; set; }
    }

    public sealed class ExecutedActionStateDto
    {
        public int Sequence { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public Guid PlayerId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
