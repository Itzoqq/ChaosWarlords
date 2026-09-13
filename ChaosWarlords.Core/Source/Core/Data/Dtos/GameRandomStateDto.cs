namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// In-memory checkpoint for the deterministic random stream used by rollback.
    /// </summary>
    public sealed class GameRandomStateDto
    {
        public ulong PcgState { get; set; }
        public int CallCount { get; set; }
    }
}
