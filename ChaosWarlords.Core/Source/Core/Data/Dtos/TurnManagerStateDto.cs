using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// In-memory checkpoint for state owned by the turn manager. This is intentionally
    /// separate from the save-game contract: command rollback must preserve the exact active
    /// turn without creating a new turn or publishing lifecycle events.
    /// </summary>
    public sealed class TurnManagerStateDto
    {
        public int CurrentPlayerIndex { get; set; }
        public PlayerColor CurrentTurnPlayerColor { get; set; }
        public PlayerColor? ForcedActingPlayerColor { get; set; }
        public TurnContextStateDto CurrentTurn { get; set; } = new();
    }
}
