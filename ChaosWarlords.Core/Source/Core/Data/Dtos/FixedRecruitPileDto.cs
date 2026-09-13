namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>Snapshot representation of one finite fixed recruit pile.</summary>
    public class FixedRecruitPileDto
    {
        public required string DefinitionId { get; set; }
        public List<CardDto> Cards { get; set; } = [];
    }
}
